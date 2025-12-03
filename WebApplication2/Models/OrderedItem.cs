namespace Rent.Models
{
    public class OrderedItem
    {
        public int OrderId { get; set; }
        public Order Order { get; set; } = null!;

        public int EquipmentId { get; set; }
        public Equipment Equipment { get; set; } = null!;

        public int Quantity { get; set; }
        public decimal PriceWhenOrdered { get; set; } 
    }
}
