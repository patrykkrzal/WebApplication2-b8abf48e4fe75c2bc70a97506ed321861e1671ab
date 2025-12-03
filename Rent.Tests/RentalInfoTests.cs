using NUnit.Framework;
using Rent.Models;
using System;
using Rent.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Rent.Tests
{
 [TestFixture]
 public class RentalInfoTests
 {
 [Test]
 public void Order_Created_WithUser_And_FoundById()
 {
 var options = new DbContextOptionsBuilder<DataContext>()
 .UseInMemoryDatabase(databaseName: "Test_Order_ById_Db")
 .Options;

 using var ctx = new DataContext(options);

 // seed RentalInfo and User
 var rentalInfo = new RentalInfo { Address = "Test", OpenHour = new TimeSpan(8,0,0), CloseHour = new TimeSpan(18,0,0) };
 ctx.RentalInfo.Add(rentalInfo);

 var user = new User { UserName = "u1", Email = "u1@example.com", First_name = "A", Last_name = "B" };
 ctx.Users.Add(user);

 ctx.SaveChanges();

 // create order linked to user and rentalInfo
 var order = new Order
 {
 Rented_Items = "Skis Small",
 OrderDate = DateTime.UtcNow,
 Price =120m,
 Date_Of_submission = DateOnly.FromDateTime(DateTime.UtcNow),
 Was_It_Returned = false,
 User = user,
 RentalInfo = rentalInfo
 };

 ctx.Orders.Add(order);
 ctx.SaveChanges();

 var id = order.Id;

 // retrieve by id
 var fetched = ctx.Orders.Include(o => o.User).Include(o => o.RentalInfo).FirstOrDefault(o => o.Id == id);

 Assert.IsNotNull(fetched, "Order should be found by id");
 Assert.AreEqual("Skis Small", fetched.Rented_Items);
 Assert.AreEqual(120m, fetched.Price);
 Assert.IsNotNull(fetched.User);
 Assert.IsNotNull(fetched.RentalInfo);
 }
 }
}
