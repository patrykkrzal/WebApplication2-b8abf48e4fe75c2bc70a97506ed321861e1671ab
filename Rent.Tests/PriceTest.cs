using NUnit.Framework;
using System;

namespace Rent.Tests
{
 [TestFixture]
 public class SeedTests
 {

 private static decimal CalculateFinalPrice(decimal basePricePerDay, int days, int itemsCount)
 {
 if (days <1) days =1;
 if (itemsCount <1) itemsCount =1;

 decimal itemsDiscount = Math.Min((itemsCount -1) *0.05m,0.20m);
 decimal daysDiscount = Math.Min((days -1) *0.05m,0.20m);
 decimal totalDiscount = itemsDiscount + daysDiscount;

 var gross = basePricePerDay * days;
 var finalPrice = gross * (1 - totalDiscount);
 return Math.Round(finalPrice,2);
 }

 [Test]
 [TestCase(100.0,3,3,240.0)]
 [TestCase(50.0,10,10,300.0)]
 public void CalculateFinalPrice_VariousCases_ReturnsExpected(double basePricePerDayD, int days, int items, double expectedD)
 {
 // arrange
 var basePricePerDay = (decimal)basePricePerDayD;
 var expected = (decimal)expectedD;

 // act
 var result = CalculateFinalPrice(basePricePerDay, days, items);

 // assert
 Assert.AreEqual(expected, result);
 }
 }
}
