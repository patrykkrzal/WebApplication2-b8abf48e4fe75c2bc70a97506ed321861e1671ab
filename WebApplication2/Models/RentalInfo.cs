using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Rent.Models
{
    public class RentalInfo
    {
        [Key]
        public int Id { get; set; } 
        public TimeSpan OpenHour { get; set; } 
        public TimeSpan CloseHour { get; set; }
        [Required]
        [MaxLength(50)]
        public string Address { get; set; }
        [MaxLength(9)]
        public string? PhoneNumber { get; set; }
        [MaxLength(255)]
        public string? OpenDays { get; set; }
        [MaxLength(255)]
        public string? Email { get; set; } 

        public ICollection<Worker> Workers { get; set; } = new List<Worker>();
        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<Equipment> Equipment { get; set; } = new List<Equipment>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
