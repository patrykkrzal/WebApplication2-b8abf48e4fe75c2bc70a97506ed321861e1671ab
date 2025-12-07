namespace Rent.DTO
{
 public class CreateOrderDto
 {
 public decimal BasePrice { get; set; }
 public int Days { get; set; }
 public int ItemsCount { get; set; }
 }
}