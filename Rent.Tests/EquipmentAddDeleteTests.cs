using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rent.Controllers;
using Rent.Data;
using Rent.DTO;
using Rent.Enums;

namespace Rent.Tests
{
 public class EquipmentAddDeleteTests
 {
 private ServiceProvider BuildServices()
 {
 var services = new ServiceCollection();
 services.AddDbContext<DataContext>(o => o.UseInMemoryDatabase("equipment_db_flat"));
 return services.BuildServiceProvider();
 }

 [Fact]
 public async Task AddAndDeleteOne_ByTypeSize_Works_Flat()
 {
 using var sp = BuildServices();
 var db = sp.GetRequiredService<DataContext>();
 var ctrl = new EquipmentController(db);

 db.Equipment.Add(new Rent.Models.Equipment { Type = EquipmentType.Skis, Size = Size.Small, Is_In_Werehouse = true, Price =120m });
 await db.SaveChangesAsync();

 var res = ctrl.DeleteOne(new CreateEquipmentDTO { Type = EquipmentType.Skis, Size = Size.Small });
 Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(res);

 var remaining = await db.Equipment.CountAsync(e => e.Type == EquipmentType.Skis && e.Size == Size.Small && e.Is_In_Werehouse && !e.Is_Reserved);
 Assert.Equal(0, remaining);
 }
 }
}
