using Rent.Models;
using System;
using System.ComponentModel.DataAnnotations;

namespace Rent.DTO
{
    public class CreateWorkerDTO
    {
        [Required, MaxLength(50)]
        public string FirstName { get; set; }   

        [Required, MaxLength(50)]
        public string LastName { get; set; }    

        [Required, MaxLength(50), EmailAddress]
        public string Email { get; set; }

        [Required, MaxLength(9)]
        public string PhoneNumber { get; set; } 

        [MaxLength(255)]
        public string? Address { get; set; }

        public TimeSpan WorkStart { get; set; }
        public TimeSpan WorkEnd { get; set; }

        [MaxLength(255)]
        public string? Working_Days { get; set; }

        [Required, MaxLength(30)]
        public string Job_Title { get; set; }

        [Required, MinLength(6)]
        public string Password { get; set; }    // DODANE (bo było brak!)  

        [Required]
        public int RentalInfoId { get; set; }
    }
}
