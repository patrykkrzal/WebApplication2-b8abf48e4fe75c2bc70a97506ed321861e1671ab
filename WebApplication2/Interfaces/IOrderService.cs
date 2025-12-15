using System.Threading.Tasks;
using Rent.Models;
using Rent.DTO;

namespace Rent.Interfaces
{
 public interface IOrderService
 {
 Order CreateOrder(CreateOrderDto dto, string userId);
 Task<(Order?, decimal)> CreateOrderAsync(CreateOrderDto dto, string userId);
 // accept/return operations
 Task<(bool Success, System.Collections.Generic.List<int> Reserved, System.DateTime? DueDate, string? Error)> AcceptAsync(int id);
 Task<(bool Success, System.Collections.Generic.List<int> Restored, string? Error)> ReturnAsync(int id);
 }
}