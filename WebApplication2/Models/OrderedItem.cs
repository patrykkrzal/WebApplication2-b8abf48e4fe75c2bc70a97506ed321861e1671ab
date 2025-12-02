namespace Rent.Models
{
    public class OrderedItem
    {
        public int OrderId { get; set; }
        public Order Order { get; set; }

        public int EquipmentId { get; set; }
        public Equipment Equipment { get; set; }

        public int Quantity { get; set; }
        public decimal PriceWhenOrdered { get; set; } 
    }
}
