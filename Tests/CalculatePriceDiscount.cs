using NUnit.Framework;

namespace Tests.Order
{
 public class CalculatePriceDiscount
    {
 [Test]
 public void CalculatePriceDiscount()
 {
 // Arrange
 decimal basePrice =100m; // price per day
 int itemsCount =2;
 int days =2;

 // Act - replicate SQL logic from fnOrderDiscount and spCalculateOrderPrice
 decimal basePct = ((itemsCount + days) *0.05m);
 decimal discount = basePct -0.10m;
 if (discount <0.00m) discount =0.00m;
 if (discount >0.40m) discount =0.40m;

 decimal multiplied = basePrice * days;
 decimal finalPrice = multiplied * (1 - discount);

 // Expected values based on the SQL logic
 Assert.AreEqual(0.10m, discount, 
 Assert.AreEqual(180.00m, finalPrice, 
 }
 }
}
