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
 // default take to200, cap at2000
 var limit = (take.HasValue && take.Value >0) ? Math.Min(take.Value,2000) :200;

 var query = _db.OrderLogs.AsQueryable();

 if (orderId.HasValue)
 query = query.Where(l => l.OrderId == orderId.Value);

 if (dateFrom.HasValue)
 {
 // use start of day
 var from = dateFrom.Value.Date;
 query = query.Where(l => l.LogDate >= from);
 }

 if (dateTo.HasValue)
 {
 // include whole day of dateTo
 var to = dateTo.Value.Date.AddDays(1).AddTicks(-1);
 query = query.Where(l => l.LogDate <= to);
 }

 if (!string.IsNullOrWhiteSpace(q))
 {
 // simple text search in Message (SQL LIKE)
 var pattern = "%" + q.Replace("%", "[%]").Replace("_", "[_]") + "%";
 query = query.Where(l => EF.Functions.Like(l.Message, pattern));
 }

 var list = await query.OrderByDescending(l => l.LogDate).Take(limit)
 .Select(l => new { l.Id, l.OrderId, l.Message, LogDate = l.LogDate })
 .ToListAsync();

 return Ok(list);
 }
 }
}
