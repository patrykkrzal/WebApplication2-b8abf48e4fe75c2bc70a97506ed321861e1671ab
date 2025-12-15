using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rent.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System;

namespace Rent.Controllers
{
 [ApiController]
 [Route("api/orderlogs")]
 [Authorize(Roles = "Admin")]
 public class OrderLogsController : ControllerBase
 {
 private readonly DataContext _db;
 public OrderLogsController(DataContext db) => _db = db;

 [HttpGet]
 public async Task<IActionResult> Get([FromQuery] int? orderId, [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo, [FromQuery] string? q, [FromQuery] int? take)
 {
 // limit
 var limit = (take.HasValue && take.Value >0) ? Math.Min(take.Value,2000) :200;

 // raw SQL logs
 // OrderLogs table is managed by DB
 var baseQuery = "SELECT Id, OrderId, Message, LogDate FROM dbo.OrderLogs";
 var whereClauses = new System.Collections.Generic.List<string>();
 var parameters = new System.Collections.Generic.List<object>();

 if (orderId.HasValue)
 {
 whereClauses.Add("OrderId = @p0");
 parameters.Add(orderId.Value);
 }
 if (dateFrom.HasValue)
 {
 whereClauses.Add("LogDate >= @p" + parameters.Count);
 parameters.Add(dateFrom.Value.Date);
 }
 if (dateTo.HasValue)
 {
 whereClauses.Add("LogDate <= @p" + parameters.Count);
 parameters.Add(dateTo.Value.Date.AddDays(1).AddTicks(-1));
 }
 if (!string.IsNullOrWhiteSpace(q))
 {
 whereClauses.Add("Message LIKE @p" + parameters.Count);
 parameters.Add("%" + q.Replace("%", "[%]").Replace("_", "[_]") + "%");
 }

 var whereSql = whereClauses.Count >0 ? (" WHERE " + string.Join(" AND ", whereClauses)) : string.Empty;
 // pagination — ensure proper spacing in OFFSET clause
 var sql = baseQuery + whereSql + " ORDER BY LogDate DESC OFFSET 0 ROWS FETCH NEXT " + limit + " ROWS ONLY";
 var list = await _db.Set<Rent.DTO.OrderLogDto>().FromSqlRaw(sql, parameters.ToArray()).ToListAsync();

 return Ok(list);
 }
 }
}
