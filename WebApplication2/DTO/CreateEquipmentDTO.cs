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
    }
}

