using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Rent;
using Rent.Models;
using Rent.Data;
using Rent.Interfaces;
using Rent.Ropository;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Rent.DTO;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // DemoMode
        static bool GetBool(string? v)
            => !string.IsNullOrWhiteSpace(v) && (v.Equals("1") || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("yes", StringComparison.OrdinalIgnoreCase));
        var demoFromCfg = builder.Configuration["DemoMode"];
        var demoFromEnv = Environment.GetEnvironmentVariable("DEMO_MODE");
        var demoMode = GetBool(demoFromEnv) || GetBool(demoFromCfg);

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
            // Per-user LocalDB
            var currentCs = builder.Configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrWhiteSpace(currentCs) && currentCs.Contains("(localdb)\\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase))
            {
                var b = new SqlConnectionStringBuilder(currentCs);
                var baseName = string.IsNullOrWhiteSpace(b.InitialCatalog) ? "RentDb" : b.InitialCatalog;
                var uname = Environment.UserName ?? "User";
                var sanitized = new string(uname.Where(char.IsLetterOrDigit).ToArray());
                if (string.IsNullOrWhiteSpace(sanitized)) sanitized = "User";
                var perUser = baseName + "_" + sanitized;
                b.InitialCatalog = perUser;
                builder.Configuration["ConnectionStrings:DefaultConnection"] = b.ToString();
                Console.WriteLine($"Using per-user LocalDB database: {perUser}");
            }
        }

        Console.WriteLine("Connection string: " + builder.Configuration.GetConnectionString("DefaultConnection"));
        Console.WriteLine("DemoMode: " + demoMode);

        // SERVICES
        builder.Services.AddControllers().AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            o.JsonSerializerOptions.PropertyNamingPolicy = null;
            o.JsonSerializerOptions.DictionaryKeyPolicy = null;
        });

        builder.Services.AddScoped<IRentalInfoRepository, RentalInfoRepository>();
        builder.Services.AddScoped<Seed>();
        builder.Services.AddDbContext<DataContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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

        // DATABASE MIGRATIONS + SP/FUNCTIONS
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
            }

            var seeder = scope.ServiceProvider.GetRequiredService<Seed>();
            seeder.SeedDataContext();
        }

        // CREATE ROLES AND ADMIN/DEMO USERS
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

            if (demoMode)
            {
                var demoEmail = "demo@demo.local";
                var demo = await userManager.FindByEmailAsync(demoEmail);
                if (demo == null)
                {
                    demo = new User { UserName = demoEmail, Email = demoEmail, First_name = "Demo", Last_name = "User" };
                    await userManager.CreateAsync(demo, "Demo1234,");
                }
            }
        }

        // SWAGGER + MIDDLEWARE
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Rent API V1"));
        app.UseHttpsRedirection();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapIdentityApi<User>();

        app.Run();
    }

    private static async Task ExecuteSqlScriptBatchedAsync(string connectionString, string script)
    {
        var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
            .Where(s => !string.IsNullOrWhiteSpace(s));
        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        foreach (var batch in batches)
        {
            using var cmd = new SqlCommand(batch, conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
