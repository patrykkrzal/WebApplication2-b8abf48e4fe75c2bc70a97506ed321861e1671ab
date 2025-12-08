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

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

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
        builder.Services.AddScoped<OrderSqlService>();
        builder.Services.AddScoped<EquipmentStateService>();
        builder.Services.AddScoped<OrderService>();

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
        });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("IsWorker", policy => policy.RequireRole("Worker"));
            options.AddPolicy("IsAdmin", policy => policy.RequireRole("Admin"));
        });

        var app = builder.Build();

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
                await ExecuteSqlScriptBatchedAsync(builder.Configuration.GetConnectionString("DefaultConnection"), script);
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
        for (var i =0; i < batches.Length; i++)
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
                if (sqlEx.Number ==2714)
                {
                    var snippet = batch.Length >1000 ? batch.Substring(0,1000) + "\n... (truncated)" : batch;
                    Console.Error.WriteLine($"Skipping SQL batch #{i +1}/{batches.Length} due to existing object (SQL Error2714): {sqlEx.Message}");
                    Console.Error.WriteLine("--- Begin skipped batch ---");
                    Console.Error.WriteLine(snippet);
                    Console.Error.WriteLine("--- End skipped batch ---");
                    // continue to next batch
                    continue;
                }

                // For other SQL exceptions, provide debug snippet and rethrow
                var snippet2 = batch.Length >1000 ? batch.Substring(0,1000) + "\n... (truncated)" : batch;
                Console.Error.WriteLine($"Failed executing SQL batch #{i +1}/{batches.Length}: {sqlEx.Message}");
                Console.Error.WriteLine("--- Begin failing batch ---");
                Console.Error.WriteLine(snippet2);
                Console.Error.WriteLine("--- End failing batch ---");
                throw;
            }
            catch (Exception ex)
            {
                // Log the batch index and a snippet of the failing SQL to help debugging
                var snippet = batch.Length >1000 ? batch.Substring(0,1000) + "\n... (truncated)" : batch;
                Console.Error.WriteLine($"Failed executing SQL batch #{i +1}/{batches.Length}: {ex.Message}");
                Console.Error.WriteLine("--- Begin failing batch ---");
                Console.Error.WriteLine(snippet);
                Console.Error.WriteLine("--- End failing batch ---");
                throw;
            }
        }
    }

    private static string GenerateTemporaryPassword(int length =12)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()-_";
        var rnd = new System.Security.Cryptography.RNGCryptoServiceProvider();
        var data = new byte[length];
        rnd.GetBytes(data);
        var result = new char[length];
        for (int i =0; i < length; i++)
        {
            var idx = data[i] % chars.Length;
            result[i] = chars[idx];
        }
        return new string(result);
    }
}
