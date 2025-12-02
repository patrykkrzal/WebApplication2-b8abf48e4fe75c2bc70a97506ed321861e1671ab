using System.ComponentModel.DataAnnotations;

namespace Rent.DTO
{
    public class RegisterUserDTO
    {

        [Required]
        [MinLength(5)]
        public string UserName { get; set; }

        [Required]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        public string Email { get; set; }

        [Required]
        [MinLength(5)]
        public string Password { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required]
        [RegularExpression(@"^\d{9}$", ErrorMessage = "Phone number must be exactly 9 digits.")]
        public string ContactNumber { get; set; }

        // Optional extras mapped to domain fields 
        public string? Login { get; set; }
    }
}
