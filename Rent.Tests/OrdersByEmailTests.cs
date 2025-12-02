using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rent.Controllers;
using Rent.Data;
using Rent.Models;

namespace Rent.Tests
{
 public class OrdersByEmailTestsFlat
 {
 private ServiceProvider BuildServices()
 {
 var services = new ServiceCollection();
 services.AddDbContext<DataContext>(o => o.UseInMemoryDatabase("orders_email_db_flat"));
 services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
 services.AddLogging();
 return services.BuildServiceProvider();
 }

 [Fact]
 public async Task GetAllOrders_ByEmail_FindsOnlyMatching_Flat()
 {
 using var sp = BuildServices();
 var db = sp.GetRequiredService<DataContext>();
 var cfg = sp.GetRequiredService<IConfiguration>();
 var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<OrdersController>();
 var ctrl = new OrdersController(db, null!, cfg, logger);

 var u1 = new User { Id = Guid.NewGuid().ToString(), Email = "flat@x.com", UserName = "flat@x.com", First_name = "Flat", Last_name = "One" };
 db.Users.Add(u1);
 db.Orders.Add(new Order { User = u1, Rented_Items = "Skis", OrderDate = DateTime.UtcNow, Price =100m });
 await db.SaveChangesAsync();

 var res = await ctrl.GetByEmail("flat@x.com") as Microsoft.AspNetCore.Mvc.OkObjectResult;
 Assert.NotNull(res);
 var list = (res!.Value as System.Collections.IEnumerable)!.Cast<object>().ToList();
 Assert.Single(list);
 }
 }
}
