using NUnit.Framework;
using Rent.Controllers;
using Rent.Enums;
using Rent.Data;
using Rent.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Rent.Tests
{
 [TestFixture]
 public class EquipmentControllerTests
 {
 [Test]
 public void EquipmentController_ResolvePrice_ReturnsExpected()
 {
 var ctrl = new EquipmentController(null!);
 var method = ctrl.GetType().GetMethod("ResolvePrice", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
 var price = (decimal)method.Invoke(ctrl, new object[] { EquipmentType.Skis, Rent.Enums.Size.Small })!;
 Assert.AreEqual(120m, price);
 }

 [Test]
 public void AddEquipment_Then_DeleteById_RemovesItem()
 {

 var options = new DbContextOptionsBuilder<DataContext>().UseInMemoryDatabase("EquipDb_AddDelete").Options;
 using var ctx = new DataContext(options);


 var eq = new Equipment
 {
 Type = EquipmentType.Skis,
 Size = Size.Small,
 Is_In_Werehouse = true,
 Is_Reserved = false,
 Price =123.45m
 };
 ctx.Equipment.Add(eq);
 ctx.SaveChanges();
 var id = eq.Id;


 var fetched = ctx.Equipment.Find(id);
 Assert.IsNotNull(fetched);

 //  delete 
 var controller = new EquipmentController(ctx);
 var res = controller.DeleteById(id);

 // Assert
 Assert.IsInstanceOf<NoContentResult>(res);
 var after = ctx.Equipment.Find(id);
 Assert.IsNull(after);
 }
 }
}
