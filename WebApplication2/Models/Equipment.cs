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

        // changed from enum to string
        [MaxLength(100)]
        public string Type { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Size { get; set; } = string.Empty;

        [Required]
        public bool Is_In_Werehouse { get; set; }

        [Required]
        public decimal Price { get; set; }

        public bool Is_Reserved { get; set; }

        public ICollection<OrderedItem> OrderedItems { get; set; } = new List<OrderedItem>();

        public RentalInfo? RentalInfo { get; set; }
        public int? RentalInfoId { get; set; }

        [MaxLength(500)]
        public string? ImageUrl { get; set; }
    }
}
