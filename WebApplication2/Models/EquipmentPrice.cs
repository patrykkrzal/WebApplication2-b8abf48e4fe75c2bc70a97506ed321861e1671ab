using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Rent.Models
{
    public class EquipmentPrice
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Type { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Size { get; set; } = string.Empty;

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        public string? Note { get; set; }

        // Navigation: all Equipment items that reference this price entry
        public ICollection<Equipment> Equipments { get; set; } = new List<Equipment>();
    }
}