using System.Collections.Generic;

namespace Rent.DTO
{
 public class ItemDetailDto
 {
 public string Type { get; set; } = string.Empty;
 public string Size { get; set; } = string.Empty;
 public int Quantity { get; set; }
 public List<int>? EquipmentIds { get; set; }
 }
}
