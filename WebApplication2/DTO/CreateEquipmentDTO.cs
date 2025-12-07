using Rent.Enums;
using System.ComponentModel.DataAnnotations;

namespace Rent.DTO
{
    public class CreateEquipmentDTO
    {
        [Required] 
        public EquipmentType Type { get; set; }
        [Required]
        public Size Size { get; set; }

        // Optional override price for new items. If null resolver provides price.
        public decimal? Price { get; set; }

        // Quantity to add/delete. Defaults to1 when null.
        [Range(1,1000)]
        public int? Quantity { get; set; }
    }
}

