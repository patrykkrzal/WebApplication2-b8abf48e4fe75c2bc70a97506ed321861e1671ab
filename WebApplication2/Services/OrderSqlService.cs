using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Rent.Services
{
 public class OrderSqlService
 {
 private readonly IConfiguration _cfg;
 public OrderSqlService(IConfiguration cfg) => _cfg = cfg;

 public async Task ExecuteCreateOrderAsync(string userId, string rentedItems, decimal basePrice, int itemsCount, int days)
 {
 var connStr = _cfg.GetConnectionString("DefaultConnection");
 using var conn = new SqlConnection(connStr);
 await conn.OpenAsync();
 using var cmd = new SqlCommand("dbo.spCreateOrder", conn)
 {
 CommandType = CommandType.StoredProcedure
 };
 cmd.Parameters.Add(new SqlParameter("@userId", SqlDbType.NVarChar,450) { Value = userId });
 cmd.Parameters.Add(new SqlParameter("@rentedItems", SqlDbType.NVarChar,255) { Value = rentedItems });
 cmd.Parameters.Add(new SqlParameter("@basePrice", SqlDbType.Decimal) { Precision =18, Scale =2, Value = basePrice });
 cmd.Parameters.Add(new SqlParameter("@itemsCount", SqlDbType.Int) { Value = itemsCount });
 cmd.Parameters.Add(new SqlParameter("@days", SqlDbType.Int) { Value = days });
 await cmd.ExecuteNonQueryAsync();
 }

 public async Task<decimal> GetOrderTotalAsync(int orderId)
 {
 var connStr = _cfg.GetConnectionString("DefaultConnection");
 using var conn = new SqlConnection(connStr);
 await conn.OpenAsync();
 using var cmd = new SqlCommand("SELECT dbo.ufn_OrderTotal(@OrderId)", conn);
 cmd.Parameters.Add(new SqlParameter("@OrderId", SqlDbType.Int) { Value = orderId });
 var totalObj = await cmd.ExecuteScalarAsync();
 return totalObj == System.DBNull.Value || totalObj == null ?0m : (decimal)totalObj;
 }

 public async Task<(decimal final, decimal pct)> CalculatePriceAsync(decimal basePrice, int itemsCount, int days)
 {
 var connStr = _cfg.GetConnectionString("DefaultConnection");
 using var conn = new SqlConnection(connStr);
 await conn.OpenAsync();
 using var calc = new SqlCommand("dbo.spCalculateOrderPrice", conn)
 {
 CommandType = CommandType.StoredProcedure
 };

 calc.Parameters.Add(new SqlParameter("@basePrice", SqlDbType.Decimal) { Precision =18, Scale =2, Value = basePrice });
 calc.Parameters.Add(new SqlParameter("@itemsCount", SqlDbType.Int) { Value = itemsCount });
 calc.Parameters.Add(new SqlParameter("@days", SqlDbType.Int) { Value = days });

 var finalParam = new SqlParameter("@finalPrice", SqlDbType.Decimal) { Precision =18, Scale =2, Direction = ParameterDirection.Output };
 var pctParam = new SqlParameter("@discountPct", SqlDbType.Decimal) { Precision =5, Scale =2, Direction = ParameterDirection.Output };
 calc.Parameters.Add(finalParam);
 calc.Parameters.Add(pctParam);

 await calc.ExecuteNonQueryAsync();
 var final = finalParam.Value == System.DBNull.Value ?0m : (decimal)finalParam.Value;
 var pct = pctParam.Value == System.DBNull.Value ?0m : (decimal)pctParam.Value;
 return (final, pct);
 }
 }
}
