using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rent.Data;
using Rent.Models;
using Xunit;

namespace Rent.Tests
{
public class OrderPriceTests
    {
    private ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
 services.AddDbContext<DataContext>(o => o.UseInMemoryDatabase("order_price_db"));
        return services.BuildServiceProvider();
   }

   [Fact]
   public async Task AddOrder_CheckPrice()
  {
 using var sp = BuildServices();
 var db = sp.GetRequiredService<DataContext>();
 var order = new Order { Rented_Items = "Skis", OrderDate = DateTime.UtcNow, Price = 123.45m };
  db.Orders.Add(order);
   await db.SaveChangesAsync();
        var fetched = await db.Orders.FirstOrDefaultAsync();
       Assert.Equal(123.45m, fetched!.Price);
  }
    }
}
