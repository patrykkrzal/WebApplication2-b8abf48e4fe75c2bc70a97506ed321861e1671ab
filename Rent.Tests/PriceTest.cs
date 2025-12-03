using NUnit.Framework;
using System;

namespace Rent.Tests
{
 [TestFixture]
 public class SeedTests
 {
 // Calculation logic matching UI:5% discount per extra item and per extra day,
 // capped at20% for items and20% for days (so max40% total).
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
 public void CalculateFinalPrice_WithThreeItemsAndThreeDays_ReturnsExpected()
 {
 // arrange
 decimal basePricePerDay =100m; // sum of item unit prices
 int days =3;
 int items =3;

 // items discount: (3-1)*5% =10%
 // days discount: (3-1)*5% =10%
 // total discount =20%
 // gross =100 *3 =300
 // final =300 * (1 -0.2) =240

 // act
 var result = CalculateFinalPrice(basePricePerDay, days, items);

 // assert
 Assert.AreEqual(240m, result);
 }

 [Test]
 public void CalculateFinalPrice_DiscountCapsApplied()
 {
 // arrange: many items and many days -> discounts capped at20% each => total40%
 decimal basePricePerDay =50m;
 int days =10; // would give (10-1)*5% =45% -> capped to20%
 int items =10; // would give (10-1)*5% =45% -> capped to20%

 // gross =50 *10 =500
 // total discount =0.2 +0.2 =0.4
 // final =500 *0.6 =300

 // act
 var result = CalculateFinalPrice(basePricePerDay, days, items);

 // assert
 Assert.AreEqual(300m, result);
 }
 }
}
