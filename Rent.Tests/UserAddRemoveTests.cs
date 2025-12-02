using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rent.Data;
using Rent.Models;
using Xunit;

namespace Rent.Tests
{
    public class UserAddRemoveTests
    {
        private ServiceProvider BuildServices()
        {
            var services = new ServiceCollection();

            // Ka¿dy test dostaje unikaln¹ bazê
            services.AddDbContext<DataContext>(o =>
                o.UseInMemoryDatabase(Guid.NewGuid().ToString()));

            services.AddIdentityCore<User>(opts =>
            {
                opts.User.RequireUniqueEmail = true;
                opts.Password.RequireDigit = true;
                opts.Password.RequireLowercase = true;
                opts.Password.RequireUppercase = true;
                opts.Password.RequiredLength = 8;
                opts.Password.RequireNonAlphanumeric = false; // jeœli nie chcesz wymagaæ znaków specjalnych
            })
            .AddEntityFrameworkStores<DataContext>()
            .AddSignInManager();

            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task AddAndRemoveUser_Succeeds()
        {
            using var sp = BuildServices();
            var userManager = sp.GetRequiredService<UserManager<User>>();

            var email = "removeuser@test.local";
            var user = new User
            {
                UserName = email,
                Email = email,
                First_name = "Test",
                Last_name = "User"
            };

            // Dodanie u¿ytkownika
            var create = await userManager.CreateAsync(user, "Test1234");
            Assert.True(create.Succeeded);

            // Pobranie u¿ytkownika
            var fetched = await userManager.FindByEmailAsync(email);
            Assert.NotNull(fetched);

            // Usuniêcie u¿ytkownika
            var delete = await userManager.DeleteAsync(fetched);
            Assert.True(delete.Succeeded);

            // Sprawdzenie, ¿e nie ma u¿ytkownika
            var after = await userManager.FindByEmailAsync(email);
            Assert.Null(after);
        }
    }
}
