using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Rent.Data;
using Rent.Models;
using System.Security.Claims;
using Rent.Enums;

namespace Rent.Controllers
{
 [ApiController]
 [Route("api/[controller]")]
 public class OrdersController : ControllerBase
 {
 private readonly DataContext _db;
 private readonly UserManager<User> _userManager;
 private readonly IConfiguration _cfg;
 private readonly ILogger<OrdersController> _logger;

 public OrdersController(DataContext db, UserManager<User> userManager, IConfiguration cfg, ILogger<OrdersController> logger)
 { _db = db; _userManager = userManager; _cfg = cfg; _logger = logger; }

 public class ItemDetailDto { public string Type { get; set; } = string.Empty; public string Size { get; set; } = string.Empty; public int Quantity { get; set; } public List<int>? EquipmentIds { get; set; } }
 public class CreateOrderDto { public string[] Items { get; set; } = Array.Empty<string>(); public decimal BasePrice { get; set; } public int Days { get; set; } =1; public int ItemsCount { get; set; } =0; public List<ItemDetailDto>? ItemsDetail { get; set; } }
 private static bool TryParseEnum<TEnum>(string value, out TEnum parsed) where TEnum : struct { parsed = default; if (string.IsNullOrWhiteSpace(value)) return false; return Enum.TryParse(value.Trim(), ignoreCase: true, out parsed); }
 private async Task<(bool ok, string? message)> ValidateStockAsync(List<ItemDetailDto>? items)
 {
 if (items == null || items.Count ==0) return (true, null);
 var grouped = items.Where(i => i != null && i.Quantity >0).GroupBy(i => new { t = (i.Type ?? string.Empty).Trim(), s = (i.Size ?? string.Empty).Trim() }).Select(g => new { g.Key.t, g.Key.s, qty = g.Sum(x => x.Quantity) }).ToList();
 foreach (var g in grouped)
 {
 if (!TryParseEnum<EquipmentType>(g.t, out var type)) return (false, $"Nieznany typ: {g.t}");
 if (!TryParseEnum<Size>(g.s, out var size)) return (false, $"Nieznany rozmiar: {g.s}");
 var available = await _db.Equipment.Where(e => e.Is_In_Werehouse && !e.Is_Reserved && e.Type == type && e.Size == size).CountAsync();
 if (g.qty > available) return (false, $"Za du¿o sztuk dla {type} {size}. Dostêpne: {available}, ¿¹dane: {g.qty}.");
 }
 return (true, null);
 }

 [Authorize]
 [HttpPost]
 public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
 {
 try{
 if (dto.Days <=0) dto.Days =1; if (dto.BasePrice <0) dto.BasePrice =0;
 var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); if (string.IsNullOrEmpty(userId)) return Unauthorized();
 var user = await _userManager.FindByIdAsync(userId); if (user == null) return Unauthorized();
 var stockCheck = await ValidateStockAsync(dto.ItemsDetail); if (!stockCheck.ok) { /* logi wy³¹czone */ return BadRequest(stockCheck.message); }
 using var conn = new SqlConnection(_cfg.GetConnectionString("DefaultConnection")); await conn.OpenAsync();
 var rentedItems = (dto.Items?.Length ??0) >0 ? string.Join(", ", dto.Items) : "Basket";
 using (var cmd = new SqlCommand("dbo.spCreateOrder", conn) { CommandType = System.Data.CommandType.StoredProcedure })
 { cmd.Parameters.Add(new SqlParameter("@userId", System.Data.SqlDbType.NVarChar,450) { Value = userId }); cmd.Parameters.Add(new SqlParameter("@rentedItems", System.Data.SqlDbType.NVarChar,255) { Value = rentedItems }); cmd.Parameters.Add(new SqlParameter("@basePrice", System.Data.SqlDbType.Decimal) { Precision =18, Scale =2, Value = dto.BasePrice }); cmd.Parameters.Add(new SqlParameter("@itemsCount", System.Data.SqlDbType.Int) { Value = dto.ItemsCount }); cmd.Parameters.Add(new SqlParameter("@days", System.Data.SqlDbType.Int) { Value = dto.Days }); await cmd.ExecuteNonQueryAsync(); }
 var order = await _db.Orders.Where(o => o.User != null && o.User.Id == userId).OrderByDescending(o => o.Id).FirstOrDefaultAsync(); if (order == null) { /* logi wy³¹czone */ return StatusCode(500, "Nie mo¿na utworzyæ zamówienia."); }
 // utrwal Days je¿eli procedura nie przypisa³a
 if ((order.Days ??0) <=0) { order.Days = dto.Days; }
 foreach (var d in dto.ItemsDetail ?? new())
 {
 if (d.Quantity <=0) continue; if (!TryParseEnum<EquipmentType>(d.Type, out var type) || !TryParseEnum<Size>(d.Size, out var size)) continue;
 var candidates = await _db.Equipment.Where(e => e.Is_In_Werehouse && !e.Is_Reserved && e.Type == type && e.Size == size).OrderBy(e => e.Id).ToListAsync(); if (candidates.Count ==0) continue;
 var picks = new List<Equipment>(); var provided = (d.EquipmentIds ?? new());
 foreach (var id in provided) { var found = candidates.FirstOrDefault(c => c.Id == id); if (found != null && !picks.Contains(found)) picks.Add(found); }
 foreach (var c in candidates) { if (picks.Count >= d.Quantity) break; if (!picks.Contains(c)) picks.Add(c); }
 picks = picks.Take(Math.Max(0, d.Quantity)).ToList();
 foreach (var eq in picks)
 { _db.Entry(eq).Property(x => x.Is_Reserved).CurrentValue = true; _db.OrderedItems.Add(new OrderedItem { OrderId = order.Id, EquipmentId = eq.Id, Quantity =1, PriceWhenOrdered = eq.Price }); }
 }
 await _db.SaveChangesAsync();
 using var calc = new SqlCommand("dbo.spCalculateOrderPrice", conn) { CommandType = System.Data.CommandType.StoredProcedure }; calc.Parameters.Add(new SqlParameter("@basePrice", System.Data.SqlDbType.Decimal) { Precision =18, Scale =2, Value = dto.BasePrice }); calc.Parameters.Add(new SqlParameter("@itemsCount", System.Data.SqlDbType.Int) { Value = dto.ItemsCount }); calc.Parameters.Add(new SqlParameter("@days", System.Data.SqlDbType.Int) { Value = dto.Days }); var finalParam = new SqlParameter("@finalPrice", System.Data.SqlDbType.Decimal) { Precision =18, Scale =2, Direction = System.Data.ParameterDirection.Output }; var pctParam = new SqlParameter("@discountPct", System.Data.SqlDbType.Decimal) { Precision =5, Scale =2, Direction = System.Data.ParameterDirection.Output }; calc.Parameters.Add(finalParam); calc.Parameters.Add(pctParam); await calc.ExecuteNonQueryAsync(); var final = (finalParam.Value == DBNull.Value) ?0m : (decimal)finalParam.Value; var pct = (pctParam.Value == DBNull.Value) ?0m : (decimal)pctParam.Value;
 /* logi wy³¹czone */
 return Ok(new { Message = "Order created", Price = final, Days = dto.Days, DiscountPct = pct, DueDate = order.OrderDate.AddDays(order.Days ??0) });
 }catch(Exception ex){ _logger.LogError(ex, "Create order failed"); /* logi wy³¹czone */ return StatusCode(500, "B³¹d tworzenia zamówienia"); }
 }

 [HttpPost("preview")]
 public async Task<IActionResult> Preview([FromBody] CreateOrderDto dto)
 { if (dto.Days <=0) dto.Days =1; if (dto.BasePrice <0) dto.BasePrice =0; var stockCheck = await ValidateStockAsync(dto.ItemsDetail); if (!stockCheck.ok) { return Ok(new { Price =0m, DiscountPct =0m, Error = stockCheck.message }); } var itemsCount = dto.ItemsCount >0 ? dto.ItemsCount : (dto.Items?.Length ??0); using var conn = new SqlConnection(_cfg.GetConnectionString("DefaultConnection")); await conn.OpenAsync(); using var calc = new SqlCommand("dbo.spCalculateOrderPrice", conn) { CommandType = System.Data.CommandType.StoredProcedure }; calc.Parameters.Add(new SqlParameter("@basePrice", System.Data.SqlDbType.Decimal) { Precision =18, Scale =2, Value = dto.BasePrice }); calc.Parameters.Add(new SqlParameter("@itemsCount", System.Data.SqlDbType.Int) { Value = itemsCount }); calc.Parameters.Add(new SqlParameter("@days", System.Data.SqlDbType.Int) { Value = dto.Days }); var finalParam = new SqlParameter("@finalPrice", System.Data.SqlDbType.Decimal) { Precision =18, Scale =2, Direction = System.Data.ParameterDirection.Output }; var pctParam = new SqlParameter("@discountPct", System.Data.SqlDbType.Decimal) { Precision =5, Scale =2, Direction = System.Data.ParameterDirection.Output }; calc.Parameters.Add(finalParam); calc.Parameters.Add(pctParam); await calc.ExecuteNonQueryAsync(); var final = (finalParam.Value == DBNull.Value) ?0m : (decimal)finalParam.Value; var pct = (pctParam.Value == DBNull.Value) ?0m : (decimal)pctParam.Value; return Ok(new { Price = final, DiscountPct = pct }); }

 [Authorize(Roles="Admin,Worker")]
 [HttpGet("pending")]
 public async Task<IActionResult> GetPending()
 {
 var list = await _db.Orders
 .AsNoTracking()
 .Include(o => o.User)
 .Include(o => o.OrderedItems).ThenInclude(oi => oi.Equipment)
 .OrderByDescending(o => o.OrderDate)
 .ToListAsync();
 var filtered = list.Where(o => !o.Was_It_Returned && o.OrderedItems.Any() && o.OrderedItems.All(oi => oi.Equipment != null && oi.Equipment.Is_Reserved && oi.Equipment.Is_In_Werehouse)).ToList();
 var result = filtered.Select(o => new{
 o.Id, o.OrderDate, o.Price,
 DueDate = o.OrderDate.AddDays(o.Days ??0),
 Days = o.Days,
 User = o.User == null ? null : new { o.User.First_name, o.User.Last_name, o.User.Email },
 Items = o.OrderedItems.Select(oi => new{ oi.EquipmentId, Type = oi.Equipment.Type.ToString(), Size = oi.Equipment.Size.ToString(), oi.Quantity, oi.PriceWhenOrdered, oi.Equipment.Is_Reserved, oi.Equipment.Is_In_Werehouse })
 });
 return Ok(result);
 }

 [Authorize(Roles="Admin,Worker")]
 [HttpGet("issued")] // wydane, oczekuj¹ na zwrot
 public async Task<IActionResult> GetIssued()
 {
 var list = await _db.Orders
 .AsNoTracking()
 .Include(o => o.User)
 .Include(o => o.OrderedItems).ThenInclude(oi => oi.Equipment)
 .OrderByDescending(o => o.OrderDate)
 .ToListAsync();
 var filtered = list.Where(o => !o.Was_It_Returned && o.OrderedItems.Any(oi => oi.Equipment != null && !oi.Equipment.Is_In_Werehouse)).ToList();
 var result = filtered.Select(o => new{
 o.Id, o.OrderDate, o.Price,
 DueDate = o.OrderDate.AddDays(o.Days ??0),
 Days = o.Days,
 User = o.User == null ? null : new { o.User.First_name, o.User.Last_name, o.User.Email },
 Items = o.OrderedItems.Select(oi => new{ oi.EquipmentId, Type = oi.Equipment.Type.ToString(), Size = oi.Equipment.Size.ToString(), oi.Quantity, oi.PriceWhenOrdered, oi.Equipment.Is_Reserved, oi.Equipment.Is_In_Werehouse })
 });
 return Ok(result);
 }

 [Authorize(Roles="Admin,Worker")]
 [HttpPost("{orderId}/accept")]
 public async Task<IActionResult> Accept(int orderId)
 {
 int affected = await _db.Database.ExecuteSqlRawAsync(
 "UPDATE e SET e.Is_In_Werehouse =0, e.Is_Reserved =1 FROM Equipment e INNER JOIN OrderedItems oi ON oi.EquipmentId = e.Id WHERE oi.OrderId = {0}", orderId);
 // nie modyfikuj OrderDate – zostaw jak wczeœniej
 var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId);
 var due = (order != null) ? order.OrderDate.AddDays(order.Days ??0) : (DateTime?)null;
 return Ok(new { Message = "Order accepted", Updated = affected, DueDate = due });
 }

 // DELETE: cancel/reject an order and release reserved equipment
 [Authorize(Roles="Admin,Worker")]
 [HttpDelete("{orderId}")]
 public async Task<IActionResult> Delete(int orderId)
 {
 var order = await _db.Orders.Include(o => o.OrderedItems).ThenInclude(oi => oi.Equipment).FirstOrDefaultAsync(o => o.Id == orderId);
 if (order == null) return NotFound();
 foreach (var oi in order.OrderedItems)
 {
 if (oi.Equipment != null)
 {
 oi.Equipment.Is_Reserved = false;
 }
 }
 _db.OrderedItems.RemoveRange(order.OrderedItems);
 _db.Orders.Remove(order);
 await _db.SaveChangesAsync();
 return Ok(new { Message = "Order deleted" });
 }

 [Authorize(Roles="Admin,Worker")]
 [HttpPost("{orderId}/returned")] // oznacz jako zwrócone
 public async Task<IActionResult> Returned(int orderId)
 {
 var order = await _db.Orders.Include(o => o.OrderedItems).ThenInclude(oi => oi.Equipment).FirstOrDefaultAsync(o => o.Id == orderId);
 if (order == null) return NotFound();
 foreach (var oi in order.OrderedItems)
 {
 if (oi.Equipment != null)
 {
 oi.Equipment.Is_In_Werehouse = true;
 oi.Equipment.Is_Reserved = false;
 }
 }
 order.Was_It_Returned = true;
 await _db.SaveChangesAsync();
 return Ok(new { Message = "Order marked as returned" });
 }

 [Authorize]
 [HttpGet]
 public async Task<IActionResult> My()
 {
 var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
 if (string.IsNullOrEmpty(userId)) return Unauthorized();
 var list = await _db.Orders
 .AsNoTracking()
 .Include(o => o.OrderedItems).ThenInclude(oi => oi.Equipment)
 .Where(o => EF.Property<string>(o, "UserId") == userId)
 .OrderByDescending(o => o.OrderDate)
 .ToListAsync();
 var shaped = list.Select(o => {
 var anyOutWhReserved = o.OrderedItems.Any(oi => oi.Equipment != null && !oi.Equipment.Is_In_Werehouse && oi.Equipment.Is_Reserved);
 string status = o.Was_It_Returned ? "Zakoñczona realizacja" : (anyOutWhReserved ? "Do oddania" : "W trakcie realizacji");
 var grouped = o.OrderedItems.Where(oi => oi.Equipment != null)
 .GroupBy(oi => new { oi.Equipment.Type, oi.Equipment.Size })
 .Select(g => new { Type = g.Key.Type.ToString(), Size = g.Key.Size.ToString(), Count = g.Count(), UnitPrice = g.First().PriceWhenOrdered });
 var dueDate = (o.Days ??0) >0 ? o.OrderDate.AddDays(o.Days!.Value) : (DateTime?)null; // zawsze jeœli mamy dni
 return new {
 o.Id,
 o.Price,
 o.OrderDate,
 DueDate = dueDate,
 o.Was_It_Returned,
 Status = status,
 Rented_Items = o.Rented_Items,
 ItemsGrouped = grouped,
 Items = o.OrderedItems.Select(oi => new {
 oi.EquipmentId,
 Type = oi.Equipment?.Type.ToString(),
 Size = oi.Equipment?.Size.ToString(),
 oi.Quantity,
 oi.PriceWhenOrdered,
 Is_In_Werehouse = oi.Equipment?.Is_In_Werehouse,
 Is_Reserved = oi.Equipment?.Is_Reserved
 })
 };
 });
 return Ok(shaped);
 }

 [Authorize(Roles="Admin,Worker")]
 [HttpGet("all")] // pe³ny raport wszystkich zamówieñ
 public async Task<IActionResult> GetAll()
 {
 var list = await _db.Orders
 .AsNoTracking()
 .Include(o => o.OrderedItems).ThenInclude(oi => oi.Equipment)
 .Include(o => o.User)
 .OrderByDescending(o => o.OrderDate)
 .ToListAsync();
 var shaped = list.Select(o => new {
 o.Id,
 o.Price,
 o.OrderDate,
 DueDate = (o.Days ??0) >0 ? o.OrderDate.AddDays(o.Days!.Value) : (DateTime?)null,
 o.Was_It_Returned,
 Status = o.Was_It_Returned ? "Zakoñczona realizacja" : (o.OrderedItems.Any(oi => oi.Equipment != null && !oi.Equipment.Is_In_Werehouse && oi.Equipment.Is_Reserved) ? "Do oddania" : "W trakcie realizacji"),
 Rented_Items = o.Rented_Items,
 User = o.User == null ? null : new { o.User.First_name, o.User.Last_name, o.User.Email },
 ItemsGrouped = o.OrderedItems.Where(oi => oi.Equipment != null).GroupBy(oi => new { oi.Equipment.Type, oi.Equipment.Size }).Select(g => new { Type = g.Key.Type.ToString(), Size = g.Key.Size.ToString(), Count = g.Count(), UnitPrice = g.First().PriceWhenOrdered }),
 Items = o.OrderedItems.Select(oi => new { oi.EquipmentId, Type = oi.Equipment?.Type.ToString(), Size = oi.Equipment?.Size.ToString(), oi.Quantity, oi.PriceWhenOrdered, Is_In_Werehouse = oi.Equipment?.Is_In_Werehouse, Is_Reserved = oi.Equipment?.Is_Reserved })
 });
 return Ok(shaped);
 }

 [Authorize(Roles="Admin,Worker")]
 [HttpGet("by-email")] // raport zamówieñ dla podanego emaila (case-insensitive)
 public async Task<IActionResult> GetByEmail([FromQuery] string email)
 {
 if(string.IsNullOrWhiteSpace(email)) return BadRequest("Email wymagalny");
 var e = email.Trim().ToLower();
 var list = await _db.Orders
 .AsNoTracking()
 .Include(o => o.OrderedItems).ThenInclude(oi => oi.Equipment)
 .Include(o => o.User)
 .Where(o => o.User != null && (
 (o.User.Email != null && o.User.Email.ToLower() == e) ||
 (o.User.UserName != null && o.User.UserName.ToLower() == e) ||
 (o.User.NormalizedEmail != null && o.User.NormalizedEmail.ToLower() == e)
 ))
 .OrderByDescending(o => o.OrderDate)
 .ToListAsync();
 var shaped = list.Select(o => new {
 o.Id,
 o.Price,
 o.OrderDate,
 DueDate = (o.Days ??0) >0 ? o.OrderDate.AddDays(o.Days!.Value) : (DateTime?)null,
 o.Was_It_Returned,
 Status = o.Was_It_Returned ? "Zakoñczona realizacja" : (o.OrderedItems.Any(oi => oi.Equipment != null && !oi.Equipment.Is_In_Werehouse && oi.Equipment.Is_Reserved) ? "Do oddania" : "W trakcie realizacji"),
 Rented_Items = o.Rented_Items,
 User = o.User == null ? null : new { o.User.First_name, o.User.Last_name, o.User.Email },
 ItemsGrouped = o.OrderedItems.Where(oi => oi.Equipment != null).GroupBy(oi => new { oi.Equipment.Type, oi.Equipment.Size }).Select(g => new { Type = g.Key.Type.ToString(), Size = g.Key.Size.ToString(), Count = g.Count(), UnitPrice = g.First().PriceWhenOrdered }),
 Items = o.OrderedItems.Select(oi => new { oi.EquipmentId, Type = oi.Equipment?.Type.ToString(), Size = oi.Equipment?.Size.ToString(), oi.Quantity, oi.PriceWhenOrdered, Is_In_Werehouse = oi.Equipment?.Is_In_Werehouse, Is_Reserved = oi.Equipment?.Is_Reserved })
 });
 return Ok(shaped);
 }

 [Authorize(Roles="Admin,Worker")]
 [HttpGet("by-id/{orderId:int}")] // raport pojedynczego zamówienia po numerze (segment)
 public async Task<IActionResult> GetById(int orderId)
 {
 var o = await _db.Orders
 .AsNoTracking()
 .Include(x => x.OrderedItems).ThenInclude(oi => oi.Equipment)
 .Include(x => x.User)
 .FirstOrDefaultAsync(x => x.Id == orderId);
 if (o == null) return NotFound("Brak zamówienia o podanym numerze.");
 var anyOutWhReserved = o.OrderedItems.Any(oi => oi.Equipment != null && !oi.Equipment.Is_In_Werehouse && oi.Equipment.Is_Reserved);
 string status = o.Was_It_Returned ? "Zakoñczona realizacja" : (anyOutWhReserved ? "Do oddania" : "W trakcie realizacji");
 var shaped = new {
 o.Id,
 o.Price,
 o.OrderDate,
 DueDate = (o.Days ??0) >0 ? o.OrderDate.AddDays(o.Days!.Value) : (DateTime?)null,
 o.Was_It_Returned,
 Status = status,
 Rented_Items = o.Rented_Items,
 User = o.User == null ? null : new { o.User.First_name, o.User.Last_name, o.User.Email },
 ItemsGrouped = o.OrderedItems.Where(oi => oi.Equipment != null).GroupBy(oi => new { oi.Equipment.Type, oi.Equipment.Size }).Select(g => new { Type = g.Key.Type.ToString(), Size = g.Key.Size.ToString(), Count = g.Count(), UnitPrice = g.First().PriceWhenOrdered }),
 Items = o.OrderedItems.Select(oi => new { oi.EquipmentId, Type = oi.Equipment?.Type.ToString(), Size = oi.Equipment?.Size.ToString(), oi.Quantity, oi.PriceWhenOrdered, Is_In_Werehouse = oi.Equipment?.Is_In_Werehouse, Is_Reserved = oi.Equipment?.Is_Reserved })
 };
 return Ok(shaped);
 }

 [Authorize(Roles="Admin,Worker")]
 [HttpGet("report")] // bezpieczna alternatywna trasa dla raportu po numerze (?id=123)
 public async Task<IActionResult> Report([FromQuery] int id)
 {
 if(id <=0) return BadRequest("Nieprawid³owy numer zamówienia.");
 var o = await _db.Orders
 .AsNoTracking()
 .Include(x => x.OrderedItems).ThenInclude(oi => oi.Equipment)
 .Include(x => x.User)
 .FirstOrDefaultAsync(x => x.Id == id);
 if (o == null) return NotFound("Brak zamówienia o podanym numerze.");
 var anyOutWhReserved = o.OrderedItems.Any(oi => oi.Equipment != null && !oi.Equipment.Is_In_Werehouse && oi.Equipment.Is_Reserved);
 string status = o.Was_It_Returned ? "Zakoñczona realizacja" : (anyOutWhReserved ? "Do oddania" : "W trakcie realizacji");
 return Ok(new {
 o.Id,
 o.Price,
 o.OrderDate,
 DueDate = (o.Days ??0) >0 ? o.OrderDate.AddDays(o.Days!.Value) : (System.DateTime?)null,
 o.Was_It_Returned,
 Status = status,
 Rented_Items = o.Rented_Items,
 User = o.User == null ? null : new { o.User.First_name, o.User.Last_name, o.User.Email },
 ItemsGrouped = o.OrderedItems.Where(oi => oi.Equipment != null).GroupBy(oi => new { oi.Equipment.Type, oi.Equipment.Size }).Select(g => new { Type = g.Key.Type.ToString(), Size = g.Key.Size.ToString(), Count = g.Count(), UnitPrice = g.First().PriceWhenOrdered }),
 Items = o.OrderedItems.Select(oi => new { oi.EquipmentId, Type = oi.Equipment?.Type.ToString(), Size = oi.Equipment?.Size.ToString(), oi.Quantity, oi.PriceWhenOrdered, Is_In_Werehouse = oi.Equipment?.Is_In_Werehouse, Is_Reserved = oi.Equipment?.Is_Reserved })
 });
 }
 }
}
