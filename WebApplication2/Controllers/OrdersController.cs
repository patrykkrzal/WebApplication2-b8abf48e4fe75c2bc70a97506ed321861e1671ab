using Microsoft.Data.SqlClient;
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
        private readonly OrderSqlService _sql;
        private readonly EquipmentStateService _equipmentState;
        private readonly OrderService _orderService;

        public OrdersController(
            DataContext db,
            UserManager<User> userManager,
            IConfiguration cfg,
            ILogger<OrdersController> logger,
            OrderSqlService sql,
            EquipmentStateService equipmentState,
            OrderService orderService)
        {
            _db = db;
            _userManager = userManager;
            _cfg = cfg;
            _logger = logger;
            _sql = sql;
            _equipmentState = equipmentState;
            _orderService = orderService;
        }

        // STOCK VALIDATION (string-based Type/Size)
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
 {
 return (false, $"Za du¿o sztuk dla {g.Type} {g.Size}. Dostêpne: {available}, ¿¹dane: {g.Qty}.");
 }
 }

 return (true, null);
 }

        // -----------------------------------------------------------------------------------------
        // CREATE ORDER
        // -----------------------------------------------------------------------------------------

 [Authorize]
 [HttpPost]
 public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
 {
 try
 {
 if (dto.Days <=0)
 dto.Days =1;

 if (dto.BasePrice <0)
 dto.BasePrice =0;

 var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
 if (string.IsNullOrEmpty(userId))
 return Unauthorized();

 var user = await _userManager.FindByIdAsync(userId);
 if (user == null)
 return Unauthorized();

 // items count fallback
 if (dto.ItemsCount <=0)
 dto.ItemsCount = System.Math.Max(0, dto.ItemsDetail?.Sum(i => i.Quantity) ??0);

 // Server-side stock validation: reject orders that request more than available
 var stockCheck = await ValidateStockAsync(dto.ItemsDetail);
 if (!stockCheck.ok)
 {
 _logger.LogWarning("Order creation blocked due to insufficient stock for user {UserId}: {Reason}", userId, stockCheck.message);
 return BadRequest(new { Message = stockCheck.message });
 }

 var rentedItems = (dto.Items?.Length ??0) >0
 ? string.Join(", ", dto.Items ?? new string[0])
 : "Basket";

 try
 {
 await _sql.ExecuteCreateOrderAsync(userId, rentedItems, dto.BasePrice, dto.ItemsCount, dto.Days);
 }
 catch (System.Exception spEx)
 {
 _logger.LogError(spEx, "Stored procedure spCreateOrder failed for user {UserId}", userId);
 // Fall back: create EF order if SP fails to keep system usable in dev
 var fallback = new Order
 {
 UserId = userId,
 Rented_Items = rentedItems,
 OrderDate = System.DateTime.UtcNow,
 Date_Of_submission = System.DateOnly.FromDateTime(System.DateTime.UtcNow),
 Was_It_Returned = false,
 BasePrice = dto.BasePrice,
 ItemsCount = dto.ItemsCount,
 Days = dto.Days,
 Price = dto.BasePrice // temporary
 };
 _db.Orders.Add(fallback);
 await _db.SaveChangesAsync();
 _logger.LogWarning("Created fallback order via EF for user {UserId}, OrderId={OrderId}", userId, fallback.Id);
 }

 // Get latest order of the user - rely on UserId set by stored procedure or fallback
 var order = await _db.Orders
 .Where(o => o.UserId == userId)
 .OrderByDescending(o => o.Id)
 .FirstOrDefaultAsync();

 if (order == null)
 {
 _logger.LogError("Order not found after create for user {UserId}", userId);
 return StatusCode(500, "Nie mo¿na utworzyæ zamówienia");
 }

 // Ensure essential fields
 if (string.IsNullOrEmpty(order.UserId)) order.UserId = userId;
 if (order.OrderDate == default) order.OrderDate = System.DateTime.UtcNow;
 if (order.Date_Of_submission == default) order.Date_Of_submission = System.DateOnly.FromDateTime(System.DateTime.UtcNow);
 order.Was_It_Returned = false;
 if ((order.Days ??0) <=0) order.Days = dto.Days;

 // Assign equipment - this logic is best-effort and non-blocking
 foreach (var d in dto.ItemsDetail ?? new())
 {
 if (d.Quantity <=0) continue;
 var typeStr = (d.Type ?? "").Trim();
 var sizeStr = (d.Size ?? "").Trim();
 if (string.IsNullOrEmpty(typeStr) || string.IsNullOrEmpty(sizeStr)) continue;

 var candidates = await _db.Equipment
 .Where(e => e.Is_In_Werehouse && !e.Is_Reserved && e.Type.ToLower() == typeStr.ToLower() && e.Size.ToLower() == sizeStr.ToLower())
 .OrderBy(e => e.Id)
 .ToListAsync();

 if (candidates.Count ==0) continue;

 var picks = new List<Equipment>();
 var provided = (d.EquipmentIds ?? new());

 foreach (var id in provided)
 {
 var found = candidates.FirstOrDefault(c => c.Id == id);
 if (found != null && !picks.Contains(found)) picks.Add(found);
 }

 foreach (var c in candidates)
 {
 if (picks.Count >= d.Quantity) break;
 if (!picks.Contains(c)) picks.Add(c);
 }

 picks = picks.Take(System.Math.Max(0, d.Quantity)).ToList();

 foreach (var eq in picks)
 {
 _db.OrderedItems.Add(new OrderedItem
 {
 OrderId = order.Id,
 EquipmentId = eq.Id,
 Quantity =1,
 PriceWhenOrdered = eq.Price ??0m
 });
 }
 }

 await _db.SaveChangesAsync();

 // final price based on OrderedItems (DB logic)
 var total = await _sql.GetOrderTotalAsync(order.Id);

 order.BasePrice = dto.BasePrice;
 order.ItemsCount = dto.ItemsCount;
 order.Days = dto.Days;
 order.Price = total;
 order.DueDate = null;

 _db.Orders.Update(order);
 await _db.SaveChangesAsync();

 _logger.LogInformation("Order created: Id={OrderId}, Final={Final}", order.Id, total);

 // ensure an audit log exists in DB (trigger/sql may have written it already)
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
 // Attempt to log failure to OrderLogs table as well
 try { await TryInsertOrderLogAsync(0, "Order creation failed: " + ex.Message); } catch { }
 return StatusCode(500, ex.Message);
 }
 }

 // Preview endpoint: calculate final price and discount without creating order
 [HttpPost("preview")]
 public async Task<IActionResult> Preview([FromBody] CreateOrderDto dto)
 {
 try
 {
 if (dto == null) dto = new CreateOrderDto();
 if (dto.Days <=0) dto.Days =1;
 if (dto.BasePrice <0) dto.BasePrice =0;
 if (dto.ItemsCount <=0) dto.ItemsCount = System.Math.Max(0, dto.ItemsDetail?.Sum(i => i.Quantity) ??0);

 // validate stock but return non-blocking warning so UI can display info while allowing checkout
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

 // --------------------------------------------------------------------
 // User endpoints
 // - GetMyOrders (GET /api/orders)
 // - ReportById (GET /api/orders/report?id=...)
 // 
 // --------------------------------------------------------------------

 [Authorize]
 [HttpGet]
 public async Task<IActionResult> GetMyOrders()
 {
 try
 {
 var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
 if (string.IsNullOrEmpty(userId))
 return Unauthorized();

 var user = await _userManager.FindByIdAsync(userId);
 var userName = user?.UserName ?? string.Empty;
 var userEmail = user?.Email ?? string.Empty;

 var ordersQuery = _db.Orders.Full();

 ordersQuery = ordersQuery.Where(o =>
 (o.UserId != null && o.UserId == userId) ||
 (o.User != null && (o.User.UserName == userName || o.User.Email == userEmail)) ||
 (!string.IsNullOrEmpty(o.Rented_Items) &&
 ((!string.IsNullOrEmpty(userName) && o.Rented_Items.Contains(userName)) ||
 (!string.IsNullOrEmpty(userEmail) && o.Rented_Items.Contains(userEmail))))) ;

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

 // ----------------------------
 // Worker/Admin endpoints
 // ----------------------------

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
 var ord = await _db.Orders.Full().FirstOrDefaultAsync(o => o.Id == id);
 if (ord == null) return NotFound("Order not found");
 if (ord.DueDate != null) return BadRequest("Order already accepted");

 // Use Days already set on the order (from creation) if present, otherwise default to7 days.
 // That lets customers who ordered for more than7 days keep their longer period.
 if ((ord.Days ??0) <=0)
 {
 ord.Days =7; // default when order didn't specify days
 }
 // Set OrderDate to now so trigger computes DueDate from acceptance moment
 ord.OrderDate = System.DateTime.UtcNow;
 ord.Was_It_Returned = false;

 _logger.LogInformation("Accepting order {OrderId}. Days={Days}", id, ord.Days);

 var reserved = new List<int>();
 foreach (var oi in ord.OrderedItems ?? Enumerable.Empty<OrderedItem>())
 {
 if (oi.Equipment != null)
 {
 _equipmentState.Reserve(oi);
 reserved.Add(oi.Equipment.Id);
 }
 else if (oi.EquipmentId !=0)
 {
 var eq = await _db.Equipment.FindAsync(oi.EquipmentId);
 if (eq != null)
 {
 eq.Is_In_Werehouse = false;
 eq.Is_Reserved = true;
 reserved.Add(eq.Id);
 }
 }
 }

 await _db.SaveChangesAsync();

 // Reload the order to get the DueDate calculated by the trigger
 var refreshed = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);

 // determine accepting worker (current user)
 string? acceptedBy = null;
 try
 {
 var actorId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
 Rent.Models.User? actor = null;
 if (!string.IsNullOrEmpty(actorId))
 {
 actor = await _userManager.FindByIdAsync(actorId);
 }

 if (actor != null)
 {
 // Prefer User.First_name/Last_name from identity user
 var first = actor.First_name ?? string.Empty;
 var last = actor.Last_name ?? string.Empty;
 acceptedBy = (first + " " + last).Trim();
 }

 // Fallback: if identity user doesn't have names, try matching a Worker by email claim
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
 catch { /* ignore */ }

 _logger.LogInformation("Order {OrderId} accepted by {AcceptedBy}. Reserved equipment: {Ids}", id, acceptedBy ?? "(unknown)", string.Join(',', reserved));

 // write acceptance audit to DB (best-effort)
 var due = refreshed?.DueDate?.ToString() ?? "(none)";
 var who = !string.IsNullOrEmpty(acceptedBy) ? $" AcceptedBy: {acceptedBy}." : "";
 await TryInsertOrderLogAsync(id, $"Order accepted. Reserved: {string.Join(',', reserved)}. DueDate: {due}.{who}");

 return Ok(new { Message = "Accepted", Reserved = reserved, ReservedCount = reserved.Count, DueDate = refreshed?.DueDate, AcceptedBy = acceptedBy });
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
 var ord = await _db.Orders.Full().FirstOrDefaultAsync(o => o.Id == id);
 if (ord == null) return NotFound("Order not found");
 if (ord.Was_It_Returned) return BadRequest("Order already marked returned");

 ord.Was_It_Returned = true;
 ord.DueDate = null;

 var restored = new List<int>();
 foreach (var oi in ord.OrderedItems ?? Enumerable.Empty<OrderedItem>())
 {
 if (oi.Equipment != null)
 {
 _equipmentState.Restore(oi);
 restored.Add(oi.Equipment.Id);
 }
 else if (oi.EquipmentId !=0)
 {
 var eq = await _db.Equipment.FindAsync(oi.EquipmentId);
 if (eq != null)
 {
 eq.Is_In_Werehouse = true;
 eq.Is_Reserved = false;
 restored.Add(eq.Id);
 }
 }
 }

 await _db.SaveChangesAsync();
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

 // Helper to insert audit log directly into DB (OrderLogs table managed by SQL triggers/scripts)
 private async Task TryInsertOrderLogAsync(int orderId, string message)
 {
 try
 {
 // Use parameterized interpolated SQL to avoid injection and to map parameters
 await _db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO dbo.OrderLogs (OrderId, Message, LogDate) VALUES ({orderId}, {message}, {DateTime.UtcNow})");
 }
 catch
 {
 // swallow - logging failure should not break main flow
 }
 }
 }
 }

