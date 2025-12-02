using System.ComponentModel.DataAnnotations;

namespace Rent.DTO
{
 public class UpdateUserDto
 {
 [Required]
 [MaxLength(256)]
 public string UserName { get; set; } = string.Empty;
 }
}
