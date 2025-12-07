using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Rent.Data;
using Rent.Models;
using System.Security.Claims;
using Rent.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

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
            public string[] Items { get; set; } = System.Array.Empty<string>();
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

            return System.Enum.TryParse(value.Trim(), ignoreCase: true, out parsed);
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
 if (dto.ItemsCount <=0)
 dto.ItemsCount = System.Math.Max(0, dto.ItemsDetail?.Sum(i => i.Quantity) ??0);

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

 // Get latest order of the user - rely on UserId set by stored procedure
 var order = await _db.Orders
 .Where(o => o.UserId == userId)
 .OrderByDescending(o => o.Id)
 .FirstOrDefaultAsync();

 if (order == null)
 {
 // If stored procedure didn't return the order, fail explicitly rather than trying multiple fallbacks
 _logger.LogError("spCreateOrder did not create an order for user {UserId}", userId);
 return StatusCode(500, "Nie mo¿na utworzyæ zamówienia przez procedurê sk³adowan¹.");
 }

 // Ensure essential fields are set in case stored procedure didn't populate them
 if (string.IsNullOrEmpty(order.UserId))
 order.UserId = userId;

 if (order.OrderDate == default)
 order.OrderDate = System.DateTime.UtcNow;

 if (order.Date_Of_submission == default)
 order.Date_Of_submission = System.DateOnly.FromDateTime(System.DateTime.UtcNow);

 // mark as not returned
 order.Was_It_Returned = false;

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

 picks = picks.Take(System.Math.Max(0, d.Quantity)).ToList();

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

 // Use SQL function to compute the final price based on OrderedItems (DB logic)
 using var totalCmd = new SqlCommand("SELECT dbo.ufn_OrderTotal(@OrderId)", conn);
 totalCmd.Parameters.Add(new SqlParameter("@OrderId", System.Data.SqlDbType.Int) { Value = order.Id });
 var totalObj = await totalCmd.ExecuteScalarAsync();
 var total = totalObj == System.DBNull.Value || totalObj == null ?0m : (decimal)totalObj;

 // Update order totals so stored order reflects calculated price/values
 order.BasePrice = dto.BasePrice;
 order.ItemsCount = dto.ItemsCount;
 order.Days = dto.Days;
 order.Price = total;

 // Ensure DueDate is not populated at creation time
 order.DueDate = null;

 _db.Orders.Update(order);
 await _db.SaveChangesAsync();

 _logger.LogInformation("Order created: Id={OrderId}, Final={Final}, BasePrice={BasePrice}, ItemsCount={ItemsCount}, Days={Days}",
 order.Id, total, order.BasePrice, order.ItemsCount, order.Days);

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
 if (dto.ItemsCount <=0) dto.ItemsCount = System.Math.Max(0, dto.ItemsDetail?.Sum(i => i.Quantity) ??0);

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

 var final = finalParam.Value == System.DBNull.Value ?0m : (decimal)finalParam.Value;
 var pct = pctParam.Value == System.DBNull.Value ?0m : (decimal)pctParam.Value;

 // Do not perform fallback calculation in application; rely on DB logic

 return Ok(new { Price = final, DiscountPct = pct });
 }
 catch (System.Exception ex)
 {
 _logger.LogError(ex, "Preview failed");
 return StatusCode(500, "B³¹d podgl¹du zamówienia");
 }
 }

 // --------------------------------------------------------------------
 // KEEP: APIs used by pages
 // - GetMyOrders (GET /api/orders)
 // - ReportById (GET /api/orders/report?id=...)
 // The other worker/admin endpoints were removed because pages don't use them.
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
 catch (System.Exception ex)
 {
 _logger.LogError(ex, "GetMyOrders failed");
 return StatusCode(500, "B³¹d pobierania zamówieñ");
 }
 }

 [Authorize(Roles = "Admin,Worker")]
 [HttpGet("report")]
 public async Task<IActionResult> ReportById([FromQuery] int id)
 {
 if (id <=0) return BadRequest("id required");
 var o = await _db.Orders
 .Include(x => x.OrderedItems)
 .ThenInclude(oi => oi.Equipment)
 .Include(x => x.User)
 .FirstOrDefaultAsync(x => x.Id == id);
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
 User = o.User != null ? new { o.User.Id, o.User.UserName, o.User.Email, FirstName = o.User.First_name, LastName = o.User.Last_name } : null,
 ItemsGrouped = o.OrderedItems
 .GroupBy(oi => new { Type = oi.Equipment.Type.ToString(), Size = oi.Equipment.Size.ToString() })
 .Select(g => new { Type = g.Key.Type, Size = g.Key.Size, Count = g.Sum(x => x.Quantity) })
 .ToList()
 };

 return Ok(obj);
 }
 }
 }

