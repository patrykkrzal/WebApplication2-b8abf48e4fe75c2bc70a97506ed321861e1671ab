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

        public decimal? Price { get; set; }

        [Range(1,1000)]
        public int? Quantity { get; set; }
    }
}

