using System.ComponentModel.DataAnnotations;

namespace Rent.DTO
{
 public class UpdateEquipmentDTO
 {

 public decimal? Price { get; set; }
 public bool? Is_In_Werehouse { get; set; }
 public bool? Is_Reserved { get; set; }
 }
}
