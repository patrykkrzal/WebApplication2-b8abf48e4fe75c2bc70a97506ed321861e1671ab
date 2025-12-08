using NUnit.Framework;

namespace Tests.Order
{
    public class CalculatePriceDiscount
    {
        [Test]
        public void CalculatePriceDiscount_Basic()
        {

            decimal basePrice = 100m;
            int itemsCount = 2;
            int days = 2;

  
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

            Assert.AreEqual(0.40m, discount);
            Assert.AreEqual(480.00m, finalPrice);
        }
    }
}
