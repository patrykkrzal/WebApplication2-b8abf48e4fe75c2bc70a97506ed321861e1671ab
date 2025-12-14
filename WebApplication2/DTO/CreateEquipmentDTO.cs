using System.ComponentModel.DataAnnotations;

namespace Rent.DTO
{
    public class CreateEquipmentDTO
    {
        [Required]
        public string Type { get; set; } = string.Empty;
        [Required]
        public string Size { get; set; } = string.Empty;

        public decimal? Price { get; set; }

        [Range(1,1000)]
        public int? Quantity { get; set; }
    }
}

