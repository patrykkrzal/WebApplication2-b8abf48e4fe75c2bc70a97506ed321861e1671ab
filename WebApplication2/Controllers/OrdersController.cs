using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Rent.Data;
using Rent.Models;
using System.Security.Claims;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Rent.DTO;
using Rent.Services;
using System;
using Microsoft.Extensions.Logging;

namespace Rent.Controllers
{
 [ApiController]
 [Route("api/[controller]")]
 public class OrdersController : ControllerBase
 {
 private readonly DataContext _db;
 private readonly UserManager<User> _userManager;
 private readonly ILogger<OrdersController> _logger;
 private readonly Rent.Interfaces.IOrderSqlService _sql;
 private readonly Rent.Interfaces.IOrderService _orderService;

 public OrdersController(
 DataContext db,
 UserManager<User> userManager,
 ILogger<OrdersController> logger,
 Rent.Interfaces.IOrderSqlService sql,
 Rent.Interfaces.IOrderService orderService)
 {
 _db = db;
 _userManager = userManager;
 _logger = logger;
 _sql = sql;
 _orderService = orderService;
 }

 // stock validation
 private async Task<(bool ok, string? message)> ValidateStockAsync(List<ItemDetailDto>? items)
 {
 if (items == null || items.Count ==0)
 return (true, null);

 var grouped = items
 .Where(i => i != null && i.Quantity >0)
 .GroupBy(i => new { t = (i.Type ?? "").Trim(), s = (i.Size ?? "").Trim() })
 .Select(g => new
 {
 Type = g.Key.t,
 Size = g.Key.s,
 Qty = g.Sum(x => x.Quantity)
 })
 .ToList();

 foreach (var g in grouped)
 {
 if (string.IsNullOrEmpty(g.Type) || string.IsNullOrEmpty(g.Size))
 return (false, $"Nieznany typ/rozmiar: '{g.Type}' / '{g.Size}'");

 var available = await _db.Equipment
 .Where(e => e.Is_In_Werehouse && !e.Is_Reserved && e.Type.ToLower() == g.Type.ToLower() && e.Size.ToLower() == g.Size.ToLower())
 .CountAsync();

 if (g.Qty > available)
 return (false, $"Za du¿o sztuk dla {g.Type} {g.Size}. Dostêpne: {available}, ¿¹dane: {g.Qty}.");
 }

 return (true, null);
 }

 [Authorize]
 [HttpPost]
 public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
 {
 try
 {
 if (dto == null) dto = new CreateOrderDto();
 if (dto.Days <=0) dto.Days =1;
 if (dto.BasePrice <0) dto.BasePrice =0;

 var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
 if (string.IsNullOrEmpty(userId)) return Unauthorized();

 // items fallback
 if (dto.ItemsCount <=0) dto.ItemsCount = System.Math.Max(0, dto.ItemsDetail?.Sum(i => i.Quantity) ??0);

 // stock check
 var stockCheck = await ValidateStockAsync(dto.ItemsDetail);
 if (!stockCheck.ok)
 {
 _logger.LogWarning("Order creation blocked due to insufficient stock for user {UserId}: {Reason}", userId, stockCheck.message);
 return BadRequest(new { Message = stockCheck.message });
 }

 var (order, total) = await _orderService.CreateOrderAsync(dto, userId);
 if (order == null)
 {
 _logger.LogError("OrderService failed to create order for user {UserId}", userId);
 return StatusCode(500, "Nie mo¿na utworzyæ zamówienia");
 }

 await TryInsertOrderLogAsync(order.Id, $"Order created by user {userId}. Final price: {total}");

 return Ok(new
 {
 Message = "Order created",
 OrderId = order.Id,
 Price = total,
 BasePrice = order.BasePrice,
 ItemsCount = order.ItemsCount,
 Days = order.Days,
 DiscountPct = (decimal?)null,
 DueDate = order.DueDate
 });
 }
 catch (System.Exception ex)
 {
 _logger.LogError(ex, "Create order failed (detailed)");
 try { await TryInsertOrderLogAsync(0, "Order creation failed: " + ex.Message); } catch { }
 return StatusCode(500, ex.Message);
 }
 }

 [HttpPost("preview")]
 public async Task<IActionResult> Preview([FromBody] CreateOrderDto dto)
 {
 try
 {
 if (dto == null) dto = new CreateOrderDto();
 if (dto.Days <=0) dto.Days =1;
 if (dto.BasePrice <0) dto.BasePrice =0;
 if (dto.ItemsCount <=0) dto.ItemsCount = System.Math.Max(0, dto.ItemsDetail?.Sum(i => i.Quantity) ??0);

 var stockCheck = await ValidateStockAsync(dto.ItemsDetail);
 var stockWarning = stockCheck.ok ? null : stockCheck.message;

 var (final, pct) = await _sql.CalculatePriceAsync(dto.BasePrice, dto.ItemsCount, dto.Days);

 if (stockWarning != null)
 return Ok(new { Price = final, DiscountPct = pct, Warning = stockWarning });

 return Ok(new { Price = final, DiscountPct = pct });
 }
 catch (System.Exception ex)
 {
 _logger.LogError(ex, "Preview failed");
 return StatusCode(500, "B³¹d podgl¹du zamówienia");
 }
 }

 [Authorize]
 [HttpGet]
 public async Task<IActionResult> GetMyOrders()
 {
 try
 {
 var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
 if (string.IsNullOrEmpty(userId)) return Unauthorized();

 var user = await _userManager.FindByIdAsync(userId);
 var userName = user?.UserName ?? string.Empty;
 var userEmail = user?.Email ?? string.Empty;

 var ordersQuery = _db.Orders.Full();

 ordersQuery = ordersQuery.Where(o =>
 (o.UserId != null && o.UserId == userId) ||
 (o.User != null && (o.User.UserName == userName || o.User.Email == userEmail)) ||
 (!string.IsNullOrEmpty(o.Rented_Items) &&
 ((!string.IsNullOrEmpty(userName) && o.Rented_Items.Contains(userName)) ||
 (!string.IsNullOrEmpty(userEmail) && o.Rented_Items.Contains(userEmail)))));

 var orders = await ordersQuery.OrderByDescending(o => o.Id).ToListAsync();

 var result = orders.Select(OrderMapper.ToDto).ToList();

 return Ok(result);
 }
 catch (System.Exception ex)
 {
 _logger.LogError(ex, "GetMyOrders failed");
 return StatusCode(500, "B³¹d pobierania zamówieñ");
 }
 }

 [Authorize(Roles = "Admin,Worker")]
 [HttpGet("pending")]
 public async Task<IActionResult> GetPending()
 {
 var list = await _db.Orders.Full()
 .Where(o => o.DueDate == null && !o.Was_It_Returned)
 .OrderByDescending(o => o.OrderDate)
 .ToListAsync();

 return Ok(list.Select(OrderMapper.ToDto));
 }

 [Authorize(Roles = "Admin,Worker")]
 [HttpGet("issued")]
 public async Task<IActionResult> GetIssued()
 {
 var list = await _db.Orders.Full()
 .Where(o => o.DueDate != null && !o.Was_It_Returned)
 .OrderByDescending(o => o.DueDate)
 .ToListAsync();

 return Ok(list.Select(OrderMapper.ToDto));
 }

 [Authorize(Roles = "Admin,Worker")]
 [HttpPost("{id}/accept")]
 public async Task<IActionResult> Accept(int id)
 {
 try
 {
 var (success, reserved, due, error) = await _orderService.AcceptAsync(id);
 if (!success)
 {
 if (error == "Order not found") return NotFound("Order not found");
 return BadRequest(error ?? "Accept failed");
 }

 string? acceptedBy = null;
 try
 {
 var actorId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
 if (!string.IsNullOrEmpty(actorId))
 {
 var actor = await _userManager.FindByIdAsync(actorId);
 if (actor != null)
 {
 var first = actor.First_name ?? string.Empty;
 var last = actor.Last_name ?? string.Empty;
 acceptedBy = (first + " " + last).Trim();
 }
 if (string.IsNullOrEmpty(acceptedBy))
 {
 var emailClaim = User.FindFirstValue(System.Security.Claims.ClaimTypes.Email) ?? actor?.Email;
 if (!string.IsNullOrEmpty(emailClaim))
 {
 var worker = await _db.Workers.FirstOrDefaultAsync(w => w.Email.ToLower() == emailClaim.ToLower());
 if (worker != null)
 {
 acceptedBy = ((worker.First_name ?? string.Empty) + " " + (worker.Last_name ?? string.Empty)).Trim();
 }
 }
 }
 }
 }
 catch { }

 _logger.LogInformation("Order {OrderId} accepted by {AcceptedBy}. Reserved equipment: {Ids}", id, acceptedBy ?? "(unknown)", string.Join(',', reserved));
 var who = !string.IsNullOrEmpty(acceptedBy) ? $" AcceptedBy: {acceptedBy}." : "";
 await TryInsertOrderLogAsync(id, $"Order accepted. Reserved: {string.Join(',', reserved)}. DueDate: {due ?? (System.DateTime?)null}.{who}");

 return Ok(new { Message = "Accepted", Reserved = reserved, ReservedCount = reserved.Count, DueDate = due, AcceptedBy = acceptedBy });
 }
 catch (System.Exception ex)
 {
 _logger.LogError(ex, "Accept failed for order {OrderId}", id);
 try { await TryInsertOrderLogAsync(id, "Accept failed: " + ex.Message); } catch { }
 return StatusCode(500, ex.Message);
 }
 }

 [Authorize(Roles = "Admin,Worker")]
 [HttpPost("{id}/returned")]
 public async Task<IActionResult> Returned(int id)
 {
 try
 {
 var (success, restored, error) = await _orderService.ReturnAsync(id);
 if (!success)
 {
 if (error == "Order not found") return NotFound("Order not found");
 return BadRequest(error ?? "Return failed");
 }

 _logger.LogInformation("Order {OrderId} returned. Restored equipment: {Ids}", id, string.Join(',', restored));
 await TryInsertOrderLogAsync(id, $"Order returned. Restored: {string.Join(',', restored)}");
 return Ok(new { Message = "Returned", Restored = restored, RestoredCount = restored.Count });
 }
 catch (System.Exception ex)
 {
 _logger.LogError(ex, "Returned failed for order {OrderId}", id);
 try { await TryInsertOrderLogAsync(id, "Returned failed: " + ex.Message); } catch { }
 return StatusCode(500, ex.Message);
 }
 }

 [Authorize(Roles = "Admin,Worker")]
 [HttpDelete("{id}")]
 public async Task<IActionResult> Delete(int id)
 {
 var ord = await _db.Orders.Include(o => o.OrderedItems).FirstOrDefaultAsync(o => o.Id == id);
 if (ord == null) return NotFound("Order not found");
 if (ord.OrderedItems != null && ord.OrderedItems.Any())
 {
 _db.OrderedItems.RemoveRange(ord.OrderedItems);
 }
 _db.Orders.Remove(ord);
 await _db.SaveChangesAsync();
 return Ok();
 }

 [Authorize(Roles = "Admin,Worker")]
 [HttpGet("by-email")]
 public async Task<IActionResult> ByEmail([FromQuery] string email)
 {
 if (string.IsNullOrWhiteSpace(email)) return BadRequest("Email required");
 var norm = email.Trim().ToLower();
 var list = await _db.Orders.Full()
 .Where(o => (o.User != null && o.User.Email.ToLower() == norm) || o.UserId == email)
 .OrderByDescending(o => o.OrderDate)
 .ToListAsync();

 return Ok(list.Select(OrderMapper.ToDto));
 }

 [Authorize(Roles = "Admin,Worker")]
 [HttpGet("all")]
 public async Task<IActionResult> All()
 {
 var list = await _db.Orders.Full()
 .OrderByDescending(o => o.OrderDate)
 .ToListAsync();

 return Ok(list.Select(OrderMapper.ToDto));
 }

 [Authorize(Roles = "Admin,Worker")]
 [HttpGet("report")]
 public async Task<IActionResult> ReportById([FromQuery] int id)
 {
 if (id <=0) return BadRequest("id required");
 var o = await _db.Orders.Full().FirstOrDefaultAsync(x => x.Id == id);
 if (o == null) return NotFound();

 var obj = new
 {
 o.Id,
 o.Rented_Items,
 o.OrderDate,
 DueDate = o.DueDate,
 o.Price,
 o.BasePrice,
 o.Days,
 o.ItemsCount,
 o.Was_It_Returned,
 User = o.User != null ? new { o.User.Id, o.User.UserName, o.User.Email, o.User.First_name, o.User.Last_name } : null,
 ItemsGrouped = o.OrderedItems
 .GroupBy(oi => new { Type = oi.Equipment.Type.ToString(), Size = oi.Equipment.Size.ToString() })
 .Select(g => new { Type = g.Key.Type, Size = g.Key.Size, Count = g.Sum(x => x.Quantity) })
 .ToList()
 };

 return Ok(obj);
 }

 // insert audit log
 private async Task TryInsertOrderLogAsync(int orderId, string message)
 {
 try
 {
 // Use parameterized interpolated SQL to avoid injection and to map parameters
 await _db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO dbo.OrderLogs (OrderId, Message, LogDate) VALUES ({orderId}, {message}, {DateTime.UtcNow})");
 }
 catch
 {

 }
 }
 }
}

