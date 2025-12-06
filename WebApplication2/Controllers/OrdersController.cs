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

        public OrdersController(
            DataContext db,
            UserManager<User> userManager,
            IConfiguration cfg,
            ILogger<OrdersController> logger)
        {
            _db = db;
            _userManager = userManager;
            _cfg = cfg;
            _logger = logger;
        }

        // DTOs
        public class ItemDetailDto
        {
            public string Type { get; set; } = string.Empty;
            public string Size { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public List<int>? EquipmentIds { get; set; }
        }

        public class CreateOrderDto
        {
            public string[] Items { get; set; } = Array.Empty<string>();
            public decimal BasePrice { get; set; }
            public int Days { get; set; } =1;
            public int ItemsCount { get; set; } =0;
            public List<ItemDetailDto>? ItemsDetail { get; set; }
        }

        private static bool TryParseEnum<TEnum>(string value, out TEnum parsed)
 where TEnum : struct
 {
 parsed = default;

 if (string.IsNullOrWhiteSpace(value))
 return false;

 return Enum.TryParse(value.Trim(), ignoreCase: true, out parsed);
 }

 // STOCK VALIDATION
 private async Task<(bool ok, string? message)> ValidateStockAsync(List<ItemDetailDto>? items)
 {
 if (items == null || items.Count ==0)
 return (true, null);

 var grouped = items
 .Where(i => i != null && i.Quantity >0)
 .GroupBy(i => new { t = (i.Type ?? "").Trim(), s = (i.Size ?? "").Trim() })
 .Select(g => new
 {
 g.Key.t,
 g.Key.s,
 qty = g.Sum(x => x.Quantity)
 })
 .ToList();

 foreach (var g in grouped)
 {
 if (!TryParseEnum<EquipmentType>(g.t, out var type))
 return (false, $"Nieznany typ: {g.t}");

 if (!TryParseEnum<Size>(g.s, out var size))
 return (false, $"Nieznany rozmiar: {g.s}");

 var available = await _db.Equipment
 .Where(e =>
 e.Is_In_Werehouse &&
 !e.Is_Reserved &&
 e.Type == type &&
 e.Size == size)
 .CountAsync();

 if (g.qty > available)
 {
 return (false,
 $"Za du¿o sztuk dla {type} {size}. Dostêpne: {available}, ¿¹dane: {g.qty}.");
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

 // stock validation
 var stockCheck = await ValidateStockAsync(dto.ItemsDetail);
 if (!stockCheck.ok)
 return BadRequest(stockCheck.message);

 // Create order via stored procedure
 using var conn = new SqlConnection(_cfg.GetConnectionString("DefaultConnection"));
 await conn.OpenAsync();

 var rentedItems = (dto.Items?.Length ??0) >0
 ? string.Join(", ", dto.Items)
 : "Basket";

 using (var cmd = new SqlCommand("dbo.spCreateOrder", conn)
 {
 CommandType = System.Data.CommandType.StoredProcedure
 })
 {
 cmd.Parameters.Add(new SqlParameter("@userId", System.Data.SqlDbType.NVarChar,450)
 {
 Value = userId
 });
 cmd.Parameters.Add(new SqlParameter("@rentedItems", System.Data.SqlDbType.NVarChar,255)
 {
 Value = rentedItems
 });
 cmd.Parameters.Add(new SqlParameter("@basePrice", System.Data.SqlDbType.Decimal)
 {
 Precision =18,
 Scale =2,
 Value = dto.BasePrice
 });
 cmd.Parameters.Add(new SqlParameter("@itemsCount", System.Data.SqlDbType.Int)
 {
 Value = dto.ItemsCount
 });
 cmd.Parameters.Add(new SqlParameter("@days", System.Data.SqlDbType.Int)
 {
 Value = dto.Days
 });

 await cmd.ExecuteNonQueryAsync();
 }

 // Get latest order of the user - filter by UserId column to avoid navigation issues
 var order = await _db.Orders
 .Where(o => o.UserId == userId)
 .OrderByDescending(o => o.Id)
 .FirstOrDefaultAsync();

 if (order == null)
 return StatusCode(500, "Nie mo¿na utworzyæ zamówienia.");

 if ((order.Days ??0) <=0)
 order.Days = dto.Days;

 // Assign equipment
 foreach (var d in dto.ItemsDetail ?? new())
 {
 if (d.Quantity <=0)
 continue;

 if (!TryParseEnum<EquipmentType>(d.Type, out var type))
 continue;

 if (!TryParseEnum<Size>(d.Size, out var size))
 continue;

 var candidates = await _db.Equipment
 .Where(e =>
 e.Is_In_Werehouse &&
 !e.Is_Reserved &&
 e.Type == type &&
 e.Size == size)
 .OrderBy(e => e.Id)
 .ToListAsync();

 if (candidates.Count ==0)
 continue;

 var picks = new List<Equipment>();
 var provided = (d.EquipmentIds ?? new());

 // Prefer chosen IDs if provided
 foreach (var id in provided)
 {
 var found = candidates.FirstOrDefault(c => c.Id == id);
 if (found != null && !picks.Contains(found))
 picks.Add(found);
 }

 // Fill the rest
 foreach (var c in candidates)
 {
 if (picks.Count >= d.Quantity)
 break;

 if (!picks.Contains(c))
 picks.Add(c);
 }

 picks = picks.Take(Math.Max(0, d.Quantity)).ToList();

 foreach (var eq in picks)
 {
 eq.Is_Reserved = true;

 _db.OrderedItems.Add(new OrderedItem
 {
 OrderId = order.Id,
 EquipmentId = eq.Id,
 Quantity =1,
 PriceWhenOrdered = eq.Price
 });
 }
 }

 await _db.SaveChangesAsync();

 // Calculate final price
 using var calc = new SqlCommand("dbo.spCalculateOrderPrice", conn)
 {
 CommandType = System.Data.CommandType.StoredProcedure
 };

 calc.Parameters.Add(new SqlParameter("@basePrice", System.Data.SqlDbType.Decimal)
 {
 Precision =18,
 Scale =2,
 Value = dto.BasePrice
 });
 calc.Parameters.Add(new SqlParameter("@itemsCount", System.Data.SqlDbType.Int)
 {
 Value = dto.ItemsCount
 });
 calc.Parameters.Add(new SqlParameter("@days", System.Data.SqlDbType.Int)
 {
 Value = dto.Days
 });

 var finalParam = new SqlParameter("@finalPrice", System.Data.SqlDbType.Decimal)
 {
 Precision =18,
 Scale =2,
 Direction = System.Data.ParameterDirection.Output
 };

 var pctParam = new SqlParameter("@discountPct", System.Data.SqlDbType.Decimal)
 {
 Precision =5,
 Scale =2,
 Direction = System.Data.ParameterDirection.Output
 };

 calc.Parameters.Add(finalParam);
 calc.Parameters.Add(pctParam);

 await calc.ExecuteNonQueryAsync();

 var final = finalParam.Value == DBNull.Value ?0m : (decimal)finalParam.Value;
 var pct = pctParam.Value == DBNull.Value ?0m : (decimal)pctParam.Value;

 return Ok(new
 {
 Message = "Order created",
 Price = final,
 Days = dto.Days,
 DiscountPct = pct,
 DueDate = order.OrderDate.AddDays(order.Days ??0)
 });
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Create order failed");
 return StatusCode(500, "B³¹d tworzenia zamówienia");
 }
 }

 // --------------------------------------------------------------------
 // DALSZE ENDPOINTY
 // --------------------------------------------------------------------

 // GET: api/orders - list orders for current user
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

 // Try to find orders by UserId first, but fall back to matches by related User or Rented_Items text
 var ordersQuery = _db.Orders
 .Include(o => o.OrderedItems)
 .ThenInclude(oi => oi.Equipment)
 .Include(o => o.User)
 .AsQueryable();

 ordersQuery = ordersQuery.Where(o =>
 (o.UserId != null && o.UserId == userId) ||
 (o.User != null && (o.User.UserName == userName || o.User.Email == userEmail)) ||
 (!string.IsNullOrEmpty(o.Rented_Items) &&
 (( !string.IsNullOrEmpty(userName) && o.Rented_Items.Contains(userName)) ||
 ( !string.IsNullOrEmpty(userEmail) && o.Rented_Items.Contains(userEmail)))));

 var orders = await ordersQuery.OrderByDescending(o => o.Id).ToListAsync();

 var result = orders.Select(o => new
 {
 o.Id,
 OrderDate = o.OrderDate,
 Price = o.Price,
 BasePrice = o.BasePrice,
 Days = o.Days,
 ItemsCount = o.ItemsCount,
 Rented_Items = o.Rented_Items,
 Was_Rejected = false,
 Was_It_Returned = o.Was_It_Returned,
 Items = o.OrderedItems.Select(oi => new
 {
 oi.EquipmentId,
 Type = oi.Equipment.Type.ToString(),
 Size = oi.Equipment.Size.ToString(),
 Quantity = oi.Quantity,
 PriceWhenOrdered = oi.PriceWhenOrdered
 }).ToList(),
 ItemsGrouped = o.OrderedItems
 .GroupBy(oi => new { Type = oi.Equipment.Type.ToString(), Size = oi.Equipment.Size.ToString() })
 .Select(g => new { Type = g.Key.Type, Size = g.Key.Size, Count = g.Sum(x => x.Quantity) })
 .ToList()
 }).ToList();

 return Ok(result);
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "GetMyOrders failed");
 return StatusCode(500, "B³¹d pobierania zamówieñ");
 }
 }

 // GET: api/orders/pending - orders awaiting issuance (items still in warehouse)
 [Authorize(Roles = "Worker,Admin")]
 [HttpGet("pending")]
 public async Task<IActionResult> GetPendingOrders()
 {
 try
 {
 var orders = await _db.Orders
 .Include(o => o.OrderedItems)
 .ThenInclude(oi => oi.Equipment)
 .Include(o => o.User)
 .Where(o => !o.Was_It_Returned) // do not include already returned orders
 .Where(o => o.OrderedItems.Any())
 .OrderByDescending(o => o.Id)
 .ToListAsync();

 var pending = orders
 .Where(o => o.OrderedItems.Any(oi => oi.Equipment != null && oi.Equipment.Is_In_Werehouse))
 .Select(o => new
 {
 o.Id,
 o.Rented_Items,
 o.OrderDate,
 Price = o.Price,
 User = o.User != null ? new { o.User.Id, o.User.UserName, o.User.Email, First_name = o.User.First_name, Last_name = o.User.Last_name } : null,
 Items = o.OrderedItems.Select(oi => new
 {
 oi.EquipmentId,
 Type = oi.Equipment?.Type.ToString(),
 Size = oi.Equipment?.Size.ToString(),
 Quantity = oi.Quantity,
 PriceWhenOrdered = oi.PriceWhenOrdered
 }).ToList()
 })
 .ToList();

 return Ok(pending);
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "GetPendingOrders failed");
 return StatusCode(500, "B³¹d pobierania oczekuj¹cych zamówieñ");
 }
 }

 // GET: api/orders/issued - orders that were issued (items taken out)
 [Authorize(Roles = "Worker,Admin")]
 [HttpGet("issued")]
 public async Task<IActionResult> GetIssuedOrders()
 {
 try
 {
 var orders = await _db.Orders
 .Include(o => o.OrderedItems)
 .ThenInclude(oi => oi.Equipment)
 .Include(o => o.User)
 .Where(o => !o.Was_It_Returned) // exclude returned
 .Where(o => o.OrderedItems.Any())
 .OrderByDescending(o => o.Id)
 .ToListAsync();

 var issued = orders
 .Where(o => o.OrderedItems.Any(oi => oi.Equipment != null && !oi.Equipment.Is_In_Werehouse))
 .Select(o => new
 {
 o.Id,
 o.Rented_Items,
 o.OrderDate,
 Price = o.Price,
 User = o.User != null ? new { o.User.Id, o.User.UserName, o.User.Email, First_name = o.User.First_name, Last_name = o.User.Last_name } : null,
 Items = o.OrderedItems.Select(oi => new
 {
 oi.EquipmentId,
 Type = oi.Equipment?.Type.ToString(),
 Size = oi.Equipment?.Size.ToString(),
 Quantity = oi.Quantity,
 PriceWhenOrdered = oi.PriceWhenOrdered
 }).ToList()
 })
 .ToList();

 return Ok(issued);
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "GetIssuedOrders failed");
 return StatusCode(500, "B³¹d pobierania wydanych zamówieñ");
 }
 }

 // POST: api/orders/{id}/accept - mark order as issued (take equipment out of warehouse)
 [Authorize(Roles = "Worker,Admin")]
 [HttpPost("{id}/accept")]
 public async Task<IActionResult> AcceptOrder(int id)
 {
 try
 {
 var order = await _db.Orders
 .Include(o => o.OrderedItems)
 .ThenInclude(oi => oi.Equipment)
 .FirstOrDefaultAsync(o => o.Id == id);

 if (order == null) return NotFound();

 if (order.Was_It_Returned)
 {
 return BadRequest(new { Message = "Zamówienie zosta³o ju¿ zwrócone i nie mo¿e byæ ponownie wydane." });
 }

 foreach (var oi in order.OrderedItems)
 {
 if (oi.Equipment != null)
 {
 oi.Equipment.Is_In_Werehouse = false; // taken out
 // keep Is_Reserved true until returned
 }
 }

 await _db.SaveChangesAsync();
 return Ok(new { Message = "Zamówienie wydane" });
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "AcceptOrder failed");
 return StatusCode(500, "B³¹d przy wydawaniu zamówienia");
 }
 }

 // POST: api/orders/{id}/returned - mark order as returned (put equipment back)
 [Authorize(Roles = "Worker,Admin")]
 [HttpPost("{id}/returned")]
 public async Task<IActionResult> MarkReturned(int id)
 {
 try
 {
 var order = await _db.Orders
 .Include(o => o.OrderedItems)
 .ThenInclude(oi => oi.Equipment)
 .FirstOrDefaultAsync(o => o.Id == id);

 if (order == null) return NotFound();

 foreach (var oi in order.OrderedItems)
 {
 if (oi.Equipment != null)
 {
 oi.Equipment.Is_In_Werehouse = true; // back to warehouse
 oi.Equipment.Is_Reserved = false; // free reservation
 }
 }

 order.Was_It_Returned = true;
 await _db.SaveChangesAsync();
 return Ok(new { Message = "Zamówienie zwrócone" });
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "MarkReturned failed");
 return StatusCode(500, "B³¹d przy oznaczaniu zwrotu");
 }
 }

 // DELETE: api/orders/{id} - delete order (cancel) and free equipment
 [Authorize(Roles = "Worker,Admin")]
 [HttpDelete("{id}")]
 public async Task<IActionResult> DeleteOrder(int id)
 {
 try
 {
 var order = await _db.Orders
 .Include(o => o.OrderedItems)
 .ThenInclude(oi => oi.Equipment)
 .FirstOrDefaultAsync(o => o.Id == id);

 if (order == null) return NotFound();

 // free equipment reservations
 foreach (var oi in order.OrderedItems)
 {
 if (oi.Equipment != null)
 {
 oi.Equipment.Is_Reserved = false;
 oi.Equipment.Is_In_Werehouse = true;
 }
 }

 // remove ordered items and order
 _db.OrderedItems.RemoveRange(order.OrderedItems);
 _db.Orders.Remove(order);

 await _db.SaveChangesAsync();
 return Ok(new { Message = "Usuniêto zamówienie" });
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "DeleteOrder failed");
 return StatusCode(500, "B³¹d usuwania zamówienia");
 }
 }
 }
}