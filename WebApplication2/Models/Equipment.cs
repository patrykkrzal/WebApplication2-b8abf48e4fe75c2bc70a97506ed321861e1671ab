using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Rent.Enums;
namespace Rent.Models
{
    public class Equipment
    {
        [Key]
        public int Id { get; set; }

        public EquipmentType Type { get; set; }

        public Size Size { get; set; }

        [Required]
        public bool Is_In_Werehouse { get; set; }

        [Required]
        public decimal Price { get; set; }

        public bool Is_Reserved { get; set; }

        public ICollection<OrderedItem> OrderedItems { get; set; } = new List<OrderedItem>();

        public RentalInfo? RentalInfo { get; set; }
        public int? RentalInfoId { get; set; }
    }
}
