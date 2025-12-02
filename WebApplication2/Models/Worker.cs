using System;
using System.ComponentModel.DataAnnotations;

namespace Rent.Models
{
    public class Worker
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(50)]
        public string First_name { get; set; }
        [Required]
        [MaxLength(50)]
        public string Last_name { get; set; }
        [Required]
        [MaxLength(50)]
        [EmailAddress]
        public string Email { get; set; }
        [Required]
        [MaxLength(9)]
        public string Phone_number { get; set; }

        [MaxLength(255)]
        public string? Address { get; set; }

        public TimeSpan WorkStart { get; set; }
        public TimeSpan WorkEnd { get; set; }
        [MaxLength(255)]
        public string? Working_Days { get; set; }
        [Required]
        [MaxLength(30)]
        public string? Job_Title { get; set; }

        [MaxLength(30)]
        public string? Role { get; set; } = "worker"; // added to match controller assignment

        public RentalInfo RentalInfo { get; set; } = null;
        public int RentalInfoId { get; set; }

        // Warehouse navigation removed
        // public Warehouse? Warehouse { get; set; }
        // public int? WarehouseId { get; set; }
    }
}

