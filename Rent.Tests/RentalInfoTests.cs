using NUnit.Framework;
using Rent.Models;
using System;

namespace Rent.Tests
{
 [TestFixture]
 public class RentalInfoTests
 {
 [Test]
 public void Order_Created_WithUser_And_HasCorrectReferences()
 {

 var rentalInfo = new RentalInfo { Address = "Test", OpenHour = new TimeSpan(8,0,0), CloseHour = new TimeSpan(18,0,0) };
 var user = new User { UserName = "u1", Email = "u1@example.com", First_name = "A", Last_name = "B" };

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

 Assert.AreEqual("Skis Small", order.Rented_Items);
 Assert.AreEqual(120m, order.Price);
 Assert.IsFalse(order.Was_It_Returned);
 Assert.IsNotNull(order.User);
 Assert.IsNotNull(order.RentalInfo);
 Assert.AreSame(user, order.User);
 Assert.AreSame(rentalInfo, order.RentalInfo);
 }
 }
}
