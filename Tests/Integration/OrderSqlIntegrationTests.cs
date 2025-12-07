using NUnit.Framework;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System;

namespace Tests.Integration
{
 [TestFixture]
 public class OrderSqlIntegrationTests
 {
 private string _connectionString = null!;

 [SetUp]
 public void Setup()
 {
 // Read connection string from environment or appsettings
 _connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
 ?? Environment.GetEnvironmentVariable("RENT_DB")
 ?? "";
 if (string.IsNullOrWhiteSpace(_connectionString)) Assert.Inconclusive("No connection string provided in env var ConnectionStrings__DefaultConnection or RENT_DB");
 }

 [Test]
 public async Task SpCalculateOrderPrice_And_FnOrderDiscount_AreCallable()
 {
 using var conn = new SqlConnection(_connectionString);
 await conn.OpenAsync();

 using (var cmd = new SqlCommand("dbo.spCalculateOrderPrice", conn) { CommandType = System.Data.CommandType.StoredProcedure })
 {
 cmd.Parameters.Add(new SqlParameter("@basePrice", System.Data.SqlDbType.Decimal) { Precision =18, Scale =2, Value =100m });
 cmd.Parameters.Add(new SqlParameter("@itemsCount", System.Data.SqlDbType.Int) { Value =2 });
 cmd.Parameters.Add(new SqlParameter("@days", System.Data.SqlDbType.Int) { Value =2 });
 var finalParam = new SqlParameter("@finalPrice", System.Data.SqlDbType.Decimal) { Precision =18, Scale =2, Direction = System.Data.ParameterDirection.Output };
 var pctParam = new SqlParameter("@discountPct", System.Data.SqlDbType.Decimal) { Precision =5, Scale =2, Direction = System.Data.ParameterDirection.Output };
 cmd.Parameters.Add(finalParam);
 cmd.Parameters.Add(pctParam);
 await cmd.ExecuteNonQueryAsync();
 var final = (decimal)finalParam.Value;
 var pct = (decimal)pctParam.Value;
 Assert.Greater(final,0m, "spCalculateOrderPrice returned zero final price");
 Assert.GreaterOrEqual(pct,0m, "spCalculateOrderPrice returned negative discount");
 }

 using (var cmd = new SqlCommand("SELECT dbo.fnOrderDiscount(@itemsCount, @days)", conn))
 {
 cmd.Parameters.Add(new SqlParameter("@itemsCount", System.Data.SqlDbType.Int) { Value =2 });
 cmd.Parameters.Add(new SqlParameter("@days", System.Data.SqlDbType.Int) { Value =2 });
 var obj = await cmd.ExecuteScalarAsync();
 var pct = obj == DBNull.Value ?0m : (decimal)obj;
 Assert.GreaterOrEqual(pct,0m);
 Assert.LessOrEqual(pct,1m);
 }
 }
 }
}
