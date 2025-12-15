using Rent.Models;
using System;
using System.ComponentModel.DataAnnotations;

namespace Rent.DTO
{
    public class CreateWorkerDTO
    {
        [Required, MaxLength(50)]
        public string FirstName { get; set; } = null!;   

        [Required, MaxLength(50)]
        public string LastName { get; set; } = null!;    

        [Required, MaxLength(50), EmailAddress]
        public string Email { get; set; } = null!;

        [Required, MaxLength(9)]
        public string PhoneNumber { get; set; } = null!; 

        [MaxLength(255)]
        public string? Address { get; set; }

        public TimeSpan WorkStart { get; set; }
        public TimeSpan WorkEnd { get; set; }

        [MaxLength(255)]
        public string? Working_Days { get; set; }

        [Required, MaxLength(30)]
        public string Job_Title { get; set; } = null!;

        [Required, MinLength(6)]
        public string Password { get; set; } = null!;    

        [Required]
        public int RentalInfoId { get; set; }
    }
}
