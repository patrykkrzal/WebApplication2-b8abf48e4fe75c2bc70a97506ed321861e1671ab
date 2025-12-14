using System.ComponentModel.DataAnnotations;

namespace Rent.DTO
{
    public class RegisterUserDTO
    {

        [Required]
        [MinLength(5)]
        public string UserName { get; set; } = null!;

        [Required]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        public string Email { get; set; } = null!;

        [Required]
        [MinLength(5)]
        public string Password { get; set; } = null!;

        [Required]
        [Compare("Password", ErrorMessage = "Hasła muszą być takie same.")]
        public string ConfirmPassword { get; set; } = null!;

        [Required]
        public string FirstName { get; set; } = null!;

        [Required]
        public string LastName { get; set; } = null!;

        [Required]
        [RegularExpression(@"^\d{9}$", ErrorMessage = "Phone number must be exactly 9 digits.")]
        public string ContactNumber { get; set; } = null!;

       
        public string? Login { get; set; }
    }
}
