using System.Threading.Tasks;

namespace Rent.Interfaces
{
 public interface IOrderSqlService
 {
 Task ExecuteCreateOrderAsync(string userId, string rentedItems, decimal basePrice, int itemsCount, int days);
 Task<decimal> GetOrderTotalAsync(int orderId);
 Task<(decimal final, decimal pct)> CalculatePriceAsync(decimal basePrice, int itemsCount, int days);
 }
}
