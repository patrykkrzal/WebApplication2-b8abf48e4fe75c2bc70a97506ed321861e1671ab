using Rent.Models;
using Rent.DTO;

namespace Rent.Interfaces
{
 public interface IOrderService
 {
 Order CreateOrder(CreateOrderDto dto, string userId);
 }
}