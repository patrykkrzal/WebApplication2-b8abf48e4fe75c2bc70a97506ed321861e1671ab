using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rent.Models
{
    // Domain user extending IdentityUser
    public class User : IdentityUser
    {
        [Required, MaxLength(50)]
        public string First_name { get; set; }

        [Required, MaxLength(50)]
        public string Last_name { get; set; }

        // Optional separate login alias (Identity uses UserName)
        [MaxLength(50)]
        public string? Login { get; set; }

        public RentalInfo? RentalInfo { get; set; }
        public int? RentalInfoId { get; set; }

        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
