using System.Collections.Generic;

namespace Rent.DTO
{
    public class CreateOrderDto
    {
        public string[] Items { get; set; } = System.Array.Empty<string>();
        public decimal BasePrice { get; set; }
        public int Days { get; set; } = 1;
        public int ItemsCount { get; set; } = 0;
        public List<ItemDetailDto>? ItemsDetail { get; set; }
    }
}