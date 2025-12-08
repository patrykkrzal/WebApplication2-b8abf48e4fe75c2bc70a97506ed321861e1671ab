using System;
using System.ComponentModel.DataAnnotations;

namespace Rent.Models
{
 public class OrderLog
 {
 [Key]
 public int Id { get; set; }

 public int OrderId { get; set; }

 [Required]
 [MaxLength(4000)]
 public string Message { get; set; } = null!;

 public DateTime LogDate { get; set; }
 }
}
