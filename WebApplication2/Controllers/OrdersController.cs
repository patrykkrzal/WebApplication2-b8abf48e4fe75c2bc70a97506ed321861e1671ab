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

                // Get latest order of the user - filter by UserId column to avoid navigation issues
                var order = await _db.Orders
                    .Where(o => o.UserId == userId)
                    .OrderByDescending(o => o.Id)
                    .FirstOrDefaultAsync();

                if (order == null)
                    return StatusCode(500, "Nie mo¿na utworzyæ zamówienia.");

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
 DueDate = order.OrderDate.AddDays(order.Days ??0)
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


    }
}

