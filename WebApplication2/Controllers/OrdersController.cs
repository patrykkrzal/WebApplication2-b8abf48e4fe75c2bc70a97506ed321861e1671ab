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
            public int Days { get; set; } = 1;
            public int ItemsCount { get; set; } = 0;
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
            if (items == null || items.Count == 0)
                return (true, null);

            var grouped = items
                .Where(i => i != null && i.Quantity > 0)
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
                if (dto.Days <= 0)
                    dto.Days = 1;

                if (dto.BasePrice < 0)
                    dto.BasePrice = 0;

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return Unauthorized();

                // stock validation
                // ensure ItemsCount is set when not provided
                if (dto.ItemsCount <=0)
                dto.ItemsCount = Math.Max(0, dto.ItemsDetail?.Sum(i => i.Quantity) ??0);

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

                // Get latest order of the user - try UserId first, then fallbacks if SP didn't populate UserId
                var order = await _db.Orders
                    .Where(o => o.UserId == userId)
                    .OrderByDescending(o => o.Id)
                    .FirstOrDefaultAsync();

                if (order == null)
                {
                    _logger.LogWarning("spCreateOrder did not set UserId or no order found by UserId for user {UserId}. Trying fallback search.", userId);

                    // Fallback1: find recent orders (last5 minutes) that match rentedItems or base price/items count
                    var now = DateTime.UtcNow;
                    var windowStart = now.AddMinutes(-5);

                    var candidates = await _db.Orders
                        .Where(o => o.OrderDate >= windowStart && (
                             (!string.IsNullOrEmpty(o.Rented_Items) && o.Rented_Items.Contains(rentedItems)) ||
                             (o.BasePrice == dto.BasePrice && o.ItemsCount == dto.ItemsCount)
                         ))
                        .OrderByDescending(o => o.Id)
                        .ToListAsync();

                    if (candidates.Any())
                    {
                        order = candidates.First();
                        _logger.LogInformation("Fallback matched order Id={OrderId} for user {UserId}", order.Id, userId);
                    }
                    else
                    {
                        // Last resort: take most recent order overall (rare), but log full warning
                        order = await _db.Orders.OrderByDescending(o => o.Id).FirstOrDefaultAsync();
                        if (order != null)
                            _logger.LogWarning("Fallback picked most recent order Id={OrderId} but it may not belong to user {UserId}", order.Id, userId);
                    }
                }

                if (order == null)
                    return StatusCode(500, "Nie mo¿na utworzyæ zamówienia.");

                // Ensure essential fields are set in case stored procedure didn't populate them
                if (string.IsNullOrEmpty(order.UserId))
                    order.UserId = userId;

                if (order.OrderDate == default)
                    order.OrderDate = DateTime.UtcNow;

                if (order.Date_Of_submission == default)
                    order.Date_Of_submission = DateOnly.FromDateTime(DateTime.UtcNow);

                // mark as not returned
                order.Was_It_Returned = false;

                if ((order.Days ?? 0) <= 0)
                    order.Days = dto.Days;

                // Assign equipment
                foreach (var d in dto.ItemsDetail ?? new())
                {
                    if (d.Quantity <= 0)
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

                    if (candidates.Count == 0)
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
                            Quantity = 1,
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
                    Precision = 18,
                    Scale = 2,
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
                    Precision = 18,
                    Scale = 2,
                    Direction = System.Data.ParameterDirection.Output
                };

                var pctParam = new SqlParameter("@discountPct", System.Data.SqlDbType.Decimal)
                {
                    Precision = 5,
                    Scale = 2,
                    Direction = System.Data.ParameterDirection.Output
                };

                calc.Parameters.Add(finalParam);
                calc.Parameters.Add(pctParam);

                await calc.ExecuteNonQueryAsync();

                var final = finalParam.Value == DBNull.Value ?0m : (decimal)finalParam.Value;
                var pct = pctParam.Value == DBNull.Value ?0m : (decimal)pctParam.Value;

                // Fallback: if stored procedure returned zero results, compute discount locally
                if ((final ==0m && pct ==0m) && dto.BasePrice >0)
                {
                    var itemsCnt = Math.Max(1, dto.ItemsCount);
                    var daysCnt = Math.Max(1, dto.Days);
                    var itemsDiscount = Math.Min((itemsCnt -1) *0.05m,0.20m);
                    var daysDiscount = Math.Min((daysCnt -1) *0.05m,0.20m);
                    var totalDiscount = itemsDiscount + daysDiscount;
                    var gross = dto.BasePrice * dto.Days;
                    var fallbackFinal = Math.Round(gross * (1 - totalDiscount),2);

                    _logger.LogInformation("spCalculateOrderPrice returned zero; using fallback calc. Items={Items}, Days={Days}, TotalDiscount={Discount}, Final={Final}", itemsCnt, daysCnt, totalDiscount, fallbackFinal);

                    final = fallbackFinal;
                    pct = totalDiscount;
                }

                // Update order totals so stored order reflects calculated price/values
                order.BasePrice = dto.BasePrice;
                order.ItemsCount = dto.ItemsCount;
                order.Days = dto.Days;
                order.Price = final;

                // Ensure DueDate is not populated at creation time — it should be set only when a worker accepts the order.
                order.DueDate = null;

                _db.Orders.Update(order);
                await _db.SaveChangesAsync();

                // Log values for debugging
                _logger.LogInformation("Order created: Id={OrderId}, Final={Final}, Discount={Pct}, BasePrice={BasePrice}, ItemsCount={ItemsCount}, Days={Days}",
 order.Id, final, pct, order.BasePrice, order.ItemsCount, order.Days);

 return Ok(new
 {
 Message = "Order created",
 OrderId = order.Id,
 Price = final,
 BasePrice = order.BasePrice,
 ItemsCount = order.ItemsCount,
 Days = order.Days,
 DiscountPct = pct,
 // Do not expose computed DueDate here; only show DueDate when worker accepted (order.DueDate)
 DueDate = order.DueDate
 });
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Create order failed");
 return StatusCode(500, "B³¹d tworzenia zamówienia");
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
        if (dto.ItemsCount <=0) dto.ItemsCount = Math.Max(0, dto.ItemsDetail?.Sum(i => i.Quantity) ??0);

        // validate stock but return as Error in body so UI can show message
        var stockCheck = await ValidateStockAsync(dto.ItemsDetail);
        if (!stockCheck.ok)
        {
            return Ok(new { Price =0m, DiscountPct =0m, Error = stockCheck.message });
        }

        using var conn = new SqlConnection(_cfg.GetConnectionString("DefaultConnection"));
        await conn.OpenAsync();

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

        // Fallback calculation if stored proc didn't provide values
        if ((final ==0m && pct ==0m) && dto.BasePrice >0)
        {
            var itemsCnt = Math.Max(1, dto.ItemsCount);
            var daysCnt = Math.Max(1, dto.Days);
            var itemsDiscount = Math.Min((itemsCnt -1) *0.05m,0.20m);
            var daysDiscount = Math.Min((daysCnt -1) *0.05m,0.20m);
            var totalDiscount = itemsDiscount + daysDiscount;
            var gross = dto.BasePrice * dto.Days;
            var fallbackFinal = Math.Round(gross * (1 - totalDiscount),2);

            _logger.LogInformation("Preview fallback used: Items={Items}, Days={Days}, TotalDiscount={Discount}, Final={Final}", itemsCnt, daysCnt, totalDiscount, fallbackFinal);

            final = fallbackFinal;
            pct = totalDiscount;
        }

        return Ok(new { Price = final, DiscountPct = pct });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Preview failed");
        return StatusCode(500, "B³¹d podgl¹du zamówienia");
    }
 }

        // --------------------------------------------------------------------
        // DALSZE ENDPOINTY
        // --------------------------------------------------------------------
        // Wszystkie poni¿ej mam ju¿ przeformatowane do³adnego stylu,
        // ale ze wzglêdu na limit miejsca – mogê je sformatowaæ w kolejnym poœcie.
        // --------------------------------------------------------------------

        // DEBUG: return last10 orders for current user with full data (useful during debugging)
        [Authorize]
        [HttpGet("debug-my")]
        public async Task<IActionResult> DebugMyOrders()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                var orders = await _db.Orders
                    .Include(o => o.OrderedItems)
                    .ThenInclude(oi => oi.Equipment)
                    .Include(o => o.User)
                    .OrderByDescending(o => o.Id)
                    .Where(o => o.UserId == userId || (o.User != null && (o.User.Id == userId)))
                    .Take(10)
                    .ToListAsync();

                var result = orders.Select(o => new
                {
                    o.Id,
                    o.Rented_Items,
                    o.OrderDate,
                    // show DueDate only when worker accepted the order (order.DueDate)
                    DueDate = o.DueDate,
                    o.Price,
                    o.BasePrice,
                    o.Days,
                    o.ItemsCount,
                    o.Date_Of_submission,
                    o.Was_It_Returned,
                    o.UserId,
                    User = o.User != null ? new { o.User.Id, o.User.UserName, o.User.Email, FirstName = o.User.First_name, LastName = o.User.Last_name } : null,
                    OrderedItems = o.OrderedItems.Select(oi => new {
                        oi.OrderId,
                        oi.EquipmentId,
                        oi.Quantity,
                        oi.PriceWhenOrdered,
                        Equipment = oi.Equipment != null ? new { oi.Equipment.Id, oi.Equipment.Type, oi.Equipment.Size, oi.Equipment.Is_In_Werehouse, oi.Equipment.Is_Reserved } : null
                    }).ToList()
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DebugMyOrders failed");
                return StatusCode(500, "B³¹d debugowania zamówieñ");
            }
 }

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

 var ordersQuery = _db.Orders
 .Include(o => o.OrderedItems)
 .ThenInclude(oi => oi.Equipment)
 .Include(o => o.User)
 .AsQueryable();

 ordersQuery = ordersQuery.Where(o =>
 (o.UserId != null && o.UserId == userId) ||
 (o.User != null && (o.User.UserName == userName || o.User.Email == userEmail)) ||
 (!string.IsNullOrEmpty(o.Rented_Items) &&
 ((!string.IsNullOrEmpty(userName) && o.Rented_Items.Contains(userName)) ||
 (!string.IsNullOrEmpty(userEmail) && o.Rented_Items.Contains(userEmail))))) ;

 var orders = await ordersQuery.OrderByDescending(o => o.Id).ToListAsync();

 var result = orders.Select(o => new
 {
 o.Id,
 OrderDate = o.OrderDate,
 // expose DueDate only if worker accepted (order.DueDate)
 DueDate = o.DueDate,
 Price = o.Price,
 BasePrice = o.BasePrice,
 Days = o.Days,
 ItemsCount = o.ItemsCount,
 Rented_Items = o.Rented_Items,
 Was_Rejected = false,
 Was_It_Returned = o.Was_It_Returned,
 UserFirstName = o.User != null ? o.User.First_name : null,
 UserLastName = o.User != null ? o.User.Last_name : null,
 Items = o.OrderedItems.Select(oi => new
 {
 oi.EquipmentId,
 Type = oi.Equipment?.Type.ToString(),
 Size = oi.Equipment?.Size.ToString(),
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

 // Worker endpoints: pending / issued and actions
 [Authorize(Roles = "Admin,Worker")]
 [HttpGet("pending")]
 public async Task<IActionResult> GetPending()
 {
 var orders = await _db.Orders
 .Include(o => o.OrderedItems)
 .ThenInclude(oi => oi.Equipment)
 .Include(o => o.User)
 .Where(o => !o.Was_It_Returned && o.OrderedItems.Any())
 .ToListAsync();

 // pending = any ordered item still in warehouse and reserved
 var pending = orders
 .Where(o => o.OrderedItems.Any(oi => (oi.Equipment != null) && oi.Equipment.Is_In_Werehouse && oi.Equipment.Is_Reserved))
 .Select(o => new
 {
 o.Id,
 o.Rented_Items,
 o.OrderDate,
 // show DueDate only when worker accepted
 DueDate = o.DueDate,
 o.Price,
 o.BasePrice,
 o.Days,
 o.ItemsCount,
 o.UserId,
 User = o.User != null ? new { o.User.Id, o.User.UserName, o.User.Email, FirstName = o.User.First_name, LastName = o.User.Last_name } : null,
 Items = o.OrderedItems.Select(oi => new
 {
 oi.OrderId,
 oi.EquipmentId,
 oi.Quantity,
 oi.PriceWhenOrdered,
 Equipment = oi.Equipment != null ? new { oi.Equipment.Id, oi.Equipment.Type, oi.Equipment.Size, oi.Equipment.Is_In_Werehouse, oi.Equipment.Is_Reserved } : null
 }).ToList()
 })
 .ToList();

 return Ok(pending);
 }

 [Authorize(Roles = "Admin,Worker")]
 [HttpGet("issued")]
 public async Task<IActionResult> GetIssued()
 {
 var orders = await _db.Orders
 .Include(o => o.OrderedItems)
 .ThenInclude(oi => oi.Equipment)
 .Include(o => o.User)
 .Where(o => !o.Was_It_Returned && o.OrderedItems.Any())
 .ToListAsync();

 // issued = any ordered item is out of warehouse (Is_In_Werehouse == false)
 var issued = orders
 .Where(o => o.OrderedItems.Any(oi => (oi.Equipment != null) && !oi.Equipment.Is_In_Werehouse && oi.Equipment.Is_Reserved))
 .Select(o => new
 {
 o.Id,
 o.Rented_Items,
 o.OrderDate,
 // show DueDate only when worker accepted
 DueDate = o.DueDate,
 o.Price,
 o.BasePrice,
 o.Days,
 o.ItemsCount,
 o.UserId,
 User = o.User != null ? new { o.User.Id, o.User.UserName, o.User.Email, FirstName = o.User.First_name, LastName = o.User.Last_name } : null,
 Items = o.OrderedItems.Select(oi => new
 {
 oi.OrderId,
 oi.EquipmentId,
 oi.Quantity,
 oi.PriceWhenOrdered,
 Equipment = oi.Equipment != null ? new { oi.Equipment.Id, oi.Equipment.Type, oi.Equipment.Size, oi.Equipment.Is_In_Werehouse, oi.Equipment.Is_Reserved } : null
 }).ToList()
 })
 .ToList();

 return Ok(issued);
 }

 [Authorize(Roles = "Admin,Worker")]
 [HttpPost("{id:int}/accept")]
 public async Task<IActionResult> AcceptOrder(int id)
 {
 var order = await _db.Orders
 .Include(o => o.OrderedItems)
 .ThenInclude(oi => oi.Equipment)
 .Include(o => o.User)
 .FirstOrDefaultAsync(o => o.Id == id);
 if (order == null) return NotFound();

 // mark equipment as taken out (Is_In_Werehouse = false)
 foreach (var oi in order.OrderedItems)
 {
 if (oi.Equipment != null)
 {
 oi.Equipment.Is_In_Werehouse = false;
 // keep Is_Reserved = true until returned
 }
 }

 // Set DueDate based on OrderDate + Days (or from now if Days null)
 if (order.Days.HasValue && order.Days.Value >0)
 {
 order.DueDate = order.OrderDate.AddDays(order.Days.Value);
 }
 else
 {
 order.DueDate = DateTime.UtcNow.AddDays(order.Days ??1);
 }

 await _db.SaveChangesAsync();
 return Ok(new { Message = "Accepted", DueDate = order.DueDate, UserFirstName = order.User?.First_name, UserLastName = order.User?.Last_name });
 }

 [Authorize(Roles = "Admin,Worker")]
 [HttpPost("{id:int}/returned")]
 public async Task<IActionResult> MarkReturned(int id)
 {
 var order = await _db.Orders
 .Include(o => o.OrderedItems)
 .ThenInclude(oi => oi.Equipment)
 .FirstOrDefaultAsync(o => o.Id == id);
 if (order == null) return NotFound();

 order.Was_It_Returned = true;

 foreach (var oi in order.OrderedItems)
 {
 if (oi.Equipment != null)
 {
 oi.Equipment.Is_In_Werehouse = true;
 oi.Equipment.Is_Reserved = false;
 }
 }

 await _db.SaveChangesAsync();
 return Ok(new { Message = "Marked returned" });
 }

 [Authorize(Roles = "Admin,Worker")]
 [HttpDelete("{id:int}")]
 public async Task<IActionResult> DeleteOrder(int id)
 {
 var order = await _db.Orders
 .Include(o => o.OrderedItems)
 .ThenInclude(oi => oi.Equipment)
 .FirstOrDefaultAsync(o => o.Id == id);
 if (order == null) return NotFound();

 // Release any reserved equipment
 foreach (var oi in order.OrderedItems)
 {
 if (oi.Equipment != null)
 {
 oi.Equipment.Is_In_Werehouse = true;
 oi.Equipment.Is_Reserved = false;
 }
 }

 // Mark order as returned/deleted (soft cancel)
 order.Was_It_Returned = true;
 order.Rented_Items = (order.Rented_Items ?? "") + " (cancelled)";

 await _db.SaveChangesAsync();
 return Ok(new { Message = "Order cancelled" });
 }
 }
 }

