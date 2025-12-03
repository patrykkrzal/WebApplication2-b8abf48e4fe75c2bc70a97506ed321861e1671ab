using NUnit.Framework;
using Rent.Models;
using System;

namespace Rent.Tests
{
 [TestFixture]
 public class OrderTests
 {
 [Test]
 public void Order_Creation_SetsPropertiesCorrectly()
 {
 var order = new Order
 {
 Rented_Items = "Skis Small",
 OrderDate = DateTime.UtcNow,
 Price =120m,
 Date_Of_submission = DateOnly.FromDateTime(DateTime.UtcNow),
 Was_It_Returned = false
 };

 Assert.AreEqual("Skis Small", order.Rented_Items);
 Assert.AreEqual(120m, order.Price);
 Assert.IsFalse(order.Was_It_Returned);
 }
 }
}
