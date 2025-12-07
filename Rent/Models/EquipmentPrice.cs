using System.ComponentModel.DataAnnotations;
using Rent.Enums;

namespace Rent.Models
{
 public class EquipmentPrice
 {
 [Key]
 public int Id { get; set; }

 [Required]
 public EquipmentType Type { get; set; }

 [Required]
 public Size Size { get; set; }

 [Required]
 [Range(0, double.MaxValue)]
 public decimal Price { get; set; }

 public string? Note { get; set; }
 }
}