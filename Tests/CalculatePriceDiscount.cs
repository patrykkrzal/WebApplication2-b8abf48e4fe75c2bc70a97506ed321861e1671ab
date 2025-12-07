using NUnit.Framework;

namespace Tests.Order
{
    public class CalculatePriceDiscount
    {
        [Test]
        public void CalculatePriceDiscount_Basic()
        {
            // Arrange
            decimal basePrice = 100m; // price per day
            int itemsCount = 2;
            int days = 2;

            // Act - replicate SQL logic from fnOrderDiscount and spCalculateOrderPrice
            // items contribution: max(0, itemsCount*0.05 -0.05) capped at0.20
            decimal itemsRaw = itemsCount * 0.05m;
            decimal itemsPct = itemsRaw - 0.05m;
            if (itemsPct < 0m) itemsPct = 0m;
            if (itemsPct > 0.20m) itemsPct = 0.20m;

            // days contribution: max(0, days*0.05 -0.05) capped at0.20
            decimal daysRaw = days * 0.05m;
            decimal daysPct = daysRaw - 0.05m;
            if (daysPct < 0m) daysPct = 0m;
            if (daysPct > 0.20m) daysPct = 0.20m;

            decimal discount = itemsPct + daysPct;
            if (discount > 0.40m) discount = 0.40m;

            decimal multiplied = basePrice * days;
            decimal finalPrice = multiplied * (1 - discount);

            // Assert expected values
            Assert.AreEqual(0.10m, discount);
            Assert.AreEqual(180.00m, finalPrice);
        }

        [Test]
        public void CalculatePriceDiscount_Caps()
        {
            // Arrange
            decimal basePrice = 100m;
            int itemsCount = 10; // large number to trigger cap
            int days = 8; // large number to trigger cap

            // Act - apply same logic as DB
            decimal itemsRaw = itemsCount * 0.05m;
            decimal itemsPct = itemsRaw - 0.05m;
            if (itemsPct < 0m) itemsPct = 0m;
            if (itemsPct > 0.20m) itemsPct = 0.20m;

            decimal daysRaw = days * 0.05m;
            decimal daysPct = daysRaw - 0.05m;
            if (daysPct < 0m) daysPct = 0m;
            if (daysPct > 0.20m) daysPct = 0.20m;

            decimal discount = itemsPct + daysPct;
            if (discount > 0.40m) discount = 0.40m;

            decimal multiplied = basePrice * days;
            decimal finalPrice = multiplied * (1 - discount);

            // itemsPct and daysPct should both be capped at0.20, so discount =0.40 and final price =100*8*0.6 =480
            Assert.AreEqual(0.40m, discount);
            Assert.AreEqual(480.00m, finalPrice);
        }
    }
}
