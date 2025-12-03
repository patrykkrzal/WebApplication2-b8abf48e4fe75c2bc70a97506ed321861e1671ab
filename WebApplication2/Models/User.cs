using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rent.Models
{
    
    public class User : IdentityUser
    {
        [Required, MaxLength(50)]
        public string First_name { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string Last_name { get; set; } = string.Empty;

    
        [MaxLength(50)]
        public string? Login { get; set; }

        public RentalInfo? RentalInfo { get; set; }
        public int? RentalInfoId { get; set; }

        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
