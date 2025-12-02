using System.ComponentModel.DataAnnotations;

namespace Rent.DTO
{
 public class UpdateEquipmentDTO
 {
 // Partial update: send only fields you want to change
 public decimal? Price { get; set; }
 public bool? Is_In_Werehouse { get; set; }
 public bool? Is_Reserved { get; set; }
 }
}
