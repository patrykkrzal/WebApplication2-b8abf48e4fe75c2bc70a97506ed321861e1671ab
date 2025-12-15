using System.Linq;
using Rent.Models;

namespace Rent.Services
{
 public static class OrderMapper
 {
 // map order to dto
 public static object ToDto(Order o)
 {
 return new
 {
 o.Id,
 OrderDate = o.OrderDate,
 DueDate = o.DueDate,
 Price = o.Price,
 BasePrice = o.BasePrice,
 Days = o.Days,
 ItemsCount = o.ItemsCount,
 Rented_Items = o.Rented_Items,
 Was_It_Returned = o.Was_It_Returned,
 User = o.User == null ? null : new { o.User.First_name, o.User.Last_name, o.User.Email },
 Items = o.OrderedItems.Select(oi => new
 {
 oi.EquipmentId,
 Type = oi.Equipment?.Type.ToString(),
 Size = oi.Equipment?.Size.ToString(),
 oi.Quantity,
 oi.PriceWhenOrdered
 }).ToList(),
 ItemsGrouped = o.OrderedItems
 .GroupBy(oi => new { Type = oi.Equipment.Type.ToString(), Size = oi.Equipment.Size.ToString() })
 .Select(g => new { Type = g.Key.Type, Size = g.Key.Size, Count = g.Sum(x => x.Quantity) })
 .ToList()
 };
 }
 }
}
