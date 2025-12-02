using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rent.Data;
using Rent.Models;
using Rent.Enums;
using Xunit;

namespace Rent.Tests
{
    public class EquipmentWarehouseTests
    {
   private ServiceProvider BuildServices()
        {
       var services = new ServiceCollection();
  services.AddDbContext<DataContext>(o => o.UseInMemoryDatabase("warehouse_db"));
   return services.BuildServiceProvider();
  }

        [Fact]
        public async Task AddEquipment_InWarehouse_IsStored()
  {
      using var sp = BuildServices();
      var db = sp.GetRequiredService<DataContext>();
      db.Equipment.Add(new Equipment { Type = EquipmentType.Skis, Size = Size.Medium, Is_In_Werehouse = true, Price = 150m });
 await db.SaveChangesAsync();
       var count = await db.Equipment.CountAsync(e => e.Is_In_Werehouse);
      Assert.Equal(1, count);
    }
    }
}
