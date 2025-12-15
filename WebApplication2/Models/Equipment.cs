using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
// using Rent.Enums;
namespace Rent.Models
{
    public class Equipment
    {
        [Key]
        public int Id { get; set; }

        // type as string
        [MaxLength(100)]
        public string Type { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Size { get; set; } = string.Empty;

        [Required]
        public bool Is_In_Werehouse { get; set; }

        // optional price
        public decimal? Price { get; set; }

        // price FK
        public int? EquipmentPriceId { get; set; }
        public EquipmentPrice? EquipmentPrice { get; set; }

        public bool Is_Reserved { get; set; }

        // relations
        public ICollection<OrderedItem> OrderedItems { get; set; } = new List<OrderedItem>();

        public RentalInfo? RentalInfo { get; set; }
        public int? RentalInfoId { get; set; }

        [MaxLength(500)]
        public string? ImageUrl { get; set; }
    }
}
