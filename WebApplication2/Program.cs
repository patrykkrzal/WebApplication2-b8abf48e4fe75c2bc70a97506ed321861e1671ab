using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Rent;
using Rent.Models;
using Rent.Data;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Rent.DTO;
using Microsoft.Extensions.FileProviders;
using Rent.Services;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using Serilog.Sinks.MSSqlServer;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using Rent.Interfaces;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure Serilog after builder is available so we can read connection string
        var logPath = Path.Combine(builder.Environment.ContentRootPath, "logs", "app-.log");
        var columnOptions = new ColumnOptions();
        // remove Properties column if you don't want it
        columnOptions.Store.Remove(StandardColumn.Properties);
        // NOTE: MessageTemplate is included by default in ColumnOptions.Store; do NOT add it again — causes duplicate column error
        // columnOptions.Store.Add(StandardColumn.MessageTemplate); // removed to avoid DuplicateNameException
        columnOptions.AdditionalColumns = new Collection<SqlColumn>
        {
            new SqlColumn("UserId", System.Data.SqlDbType.NVarChar, dataLength:450),
            new SqlColumn("RequestPath", System.Data.SqlDbType.NVarChar, dataLength:1000)
        };

        var connStr = builder.Configuration.GetConnectionString("DefaultConnection");

        var loggerCfg = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(new JsonFormatter(), logPath, rollingInterval: RollingInterval.Day);

        if (!string.IsNullOrWhiteSpace(connStr))
        {
            loggerCfg = loggerCfg.WriteTo.MSSqlServer(
                connectionString: connStr,
                sinkOptions: new MSSqlServerSinkOptions { TableName = "AppLogs", AutoCreateSqlTable = true },
                columnOptions: columnOptions);
        }

        Log.Logger = loggerCfg.CreateLogger();

        // replace default logging with Serilog
        builder.Host.UseSerilog();

        // CONNECTION STRING OVERRIDES
        var rentDbEnv = Environment.GetEnvironmentVariable("RENT_DB");
        var csEnv = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(csEnv))
        {
            builder.Configuration["ConnectionStrings:DefaultConnection"] = csEnv;
            Console.WriteLine("Using ConnectionStrings__DefaultConnection from environment.");
        }
        else if (!string.IsNullOrWhiteSpace(rentDbEnv))
        {
            builder.Configuration["ConnectionStrings:DefaultConnection"] = rentDbEnv;
            Console.WriteLine("Using RENT_DB from environment.");
        }
        else
        {
            // No per-user override: use the DefaultConnection from configuration or environment as-is.
            // If you want per-user LocalDB behavior, enable it explicitly via configuration.
        }

        Console.WriteLine("Connection string: " + builder.Configuration.GetConnectionString("DefaultConnection"));


        builder.Services.AddControllers().AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            o.JsonSerializerOptions.PropertyNamingPolicy = null;
            o.JsonSerializerOptions.DictionaryKeyPolicy = null;
        });

        builder.Services.AddRazorPages();

        builder.Services.AddScoped<Seed>();
        builder.Services.AddDbContext<DataContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<IPriceResolver, EfPriceResolver>();
        builder.Services.AddScoped<IOrderSqlService, OrderSqlService>();
        builder.Services.AddScoped<IEquipmentStateService, EquipmentStateService>();
        builder.Services.AddScoped<IOrderService, OrderService>();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Rent API", Version = "v1" });
        });

        builder.Services.AddIdentityCore<User>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 8;
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<DataContext>()
        .AddSignInManager()
        .AddApiEndpoints();

        builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddBearerToken(IdentityConstants.BearerScheme)
            .AddCookie(IdentityConstants.ApplicationScheme);

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            // Serve the app's static login page instead of default /Account/Login
            options.LoginPath = "/Login.html";
            options.AccessDeniedPath = "/Login.html";
        });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("IsWorker", policy => policy.RequireRole("Worker"));
            options.AddPolicy("IsAdmin", policy => policy.RequireRole("Admin"));
        });

        var app = builder.Build();

        // Enrich logs with user id and request path
        app.Use(async (context, next) =>
        {
            var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            using (Serilog.Context.LogContext.PushProperty("UserId", userId))
            using (Serilog.Context.LogContext.PushProperty("RequestPath", context.Request?.Path.Value))
            {
                await next();
            }
        });

        //MIGRATIONS 
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();

            context.Database.Migrate();

            var sqlPathCandidates = new[]
            {
                Path.Combine(app.Environment.ContentRootPath, "DatabaseObjects.sql"),
                Path.Combine(app.Environment.ContentRootPath, "WebApplication2", "DatabaseObjects.sql")
            };
            string? sqlPath = sqlPathCandidates.FirstOrDefault(File.Exists);
            if (sqlPath != null)
            {
                var script = await File.ReadAllTextAsync(sqlPath);
                var connStrLocal = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
                await ExecuteSqlScriptBatchedAsync(connStrLocal, script);
                Console.WriteLine($"Executed DatabaseObjects.sql from '{sqlPath}'.");

                var seeder = scope.ServiceProvider.GetRequiredService<Seed>();
                seeder.SeedDataContext();
            }
        }

        //ADMIN/USERS/WORKERS ROLES
        using (var scope = app.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in new[] { "Worker", "User", "Admin" })
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        using (var scope = app.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            string email = "admin@admin.com";
            string password = "Test1234,";

            if (await userManager.FindByEmailAsync(email) == null)
            {
                var user = new User { UserName = email, Email = email, First_name = "Admin", Last_name = "Admin" };
                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(user, "Admin");
            }
        }


        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Rent API V1"));
        app.UseHttpsRedirection();
        app.UseDefaultFiles();
        app.UseStaticFiles();


        var pagesStaticPath = Path.Combine(app.Environment.ContentRootPath, "Pages");
        if (Directory.Exists(pagesStaticPath))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(pagesStaticPath),
                RequestPath = ""
            });
        }

        app.UseRouting();
        app.UseAuthentication();
        // Intercept default Identity redirects to /Account/Login and rewrite to static /Login.html
        app.Use(async (context, next) =>
        {
            await next();
            try
            {
                if (context.Response.StatusCode == 302 && context.Response.Headers.ContainsKey("Location"))
                {
                    var loc = context.Response.Headers["Location"].ToString();
                    if (!string.IsNullOrEmpty(loc) && loc.IndexOf("/Account/Login", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // Replace the Identity UI path with the app's static login page
                        var newLoc = loc.Replace("/Account/Login", "/Login.html");
                        context.Response.Headers["Location"] = newLoc;
                    }
                }
            }
            catch { }
        });
        app.UseAuthorization();
        app.MapControllers();
        app.MapIdentityApi<User>();

        app.MapRazorPages();

        app.Run();
    }
    private static async Task ExecuteSqlScriptBatchedAsync(string connectionString, string script)
    {
        var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        for (var i = 0; i < batches.Length; i++)
        {
            var batch = batches[i];
            try
            {
                using var cmd = new SqlCommand(batch, conn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException sqlEx)
            {
                // If object already exists (2714), log and continue with next batch instead of failing whole migration.
                if (sqlEx.Number == 2714)
                {
                    var snippet = batch.Length > 1000 ? batch.Substring(0, 1000) + "\n... (truncated)" : batch;
                    Console.Error.WriteLine($"Skipping SQL batch #{i + 1}/{batches.Length} due to existing object (SQL Error2714): {sqlEx.Message}");
                    Console.Error.WriteLine("--- Begin skipped batch ---");
                    Console.Error.WriteLine(snippet);
                    Console.Error.WriteLine("--- End skipped batch ---");
                    // continue to next batch
                    continue;
                }

                // For other SQL exceptions, provide debug snippet and rethrow
                var snippet2 = batch.Length > 1000 ? batch.Substring(0, 1000) + "\n... (truncated)" : batch;
                Console.Error.WriteLine($"Failed executing SQL batch #{i + 1}/{batches.Length}: {sqlEx.Message}");
                Console.Error.WriteLine("--- Begin failing batch ---");
                Console.Error.WriteLine(snippet2);
                Console.Error.WriteLine("--- End failing batch ---");
                throw;
            }
            catch (Exception ex)
            {
                // Log the batch index and a snippet of the failing SQL to help debugging
                var snippet = batch.Length > 1000 ? batch.Substring(0, 1000) + "\n... (truncated)" : batch;
                Console.Error.WriteLine($"Failed executing SQL batch #{i + 1}/{batches.Length}: {ex.Message}");
                Console.Error.WriteLine("--- Begin failing batch ---");
                Console.Error.WriteLine(snippet);
                Console.Error.WriteLine("--- End failing batch ---");
                throw;
            }
        }
    }

    private static string GenerateTemporaryPassword(int length = 12)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()-_";
        using (var rng = RandomNumberGenerator.Create())
        {
            var data = new byte[length];
            rng.GetBytes(data);
            var result = new char[length];
            for (int i = 0; i < length; i++)
            {
                var idx = data[i] % chars.Length;
                result[i] = chars[idx];
            }
            return new string(result);
        }
    }
}
