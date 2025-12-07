using Rent.Interfaces;
using Rent.DTO;
using Rent.Models;

namespace Rent.Services
{
 public class OrderService : IOrderService
 {
 public Order CreateOrder(CreateOrderDto dto, string userId)
 {
 return new Order
 {
 Id =1,
 UserId = userId,
 BasePrice = dto.BasePrice,
 Days = dto.Days,
 Price = dto.BasePrice * dto.Days,
 Rented_Items = "auto",
 Date_Of_submission = System.DateOnly.FromDateTime(System.DateTime.UtcNow),
 Was_It_Returned = false
 };
 }
 }
}