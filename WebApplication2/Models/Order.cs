using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Rent.Models
{
    public class Order
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Rented_Items { get; set; }

        public DateTime OrderDate { get; set; }

        [Required]
        public decimal Price { get; set; }

        // store calculation inputs for UI/history
        public decimal? BasePrice { get; set; }
        public int? Days { get; set; }
        public int? ItemsCount { get; set; }

        [Required]
        public DateOnly Date_Of_submission { get; set; }

        [Required]
        public bool Was_It_Returned { get; set; }

        public ICollection<OrderedItem> OrderedItems { get; set; } = new List<OrderedItem>();

        public User? User { get; set; }

        public RentalInfo? RentalInfo { get; set; }
    }
}
