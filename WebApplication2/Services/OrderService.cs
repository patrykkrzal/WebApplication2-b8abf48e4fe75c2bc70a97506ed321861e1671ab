using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Rent.Interfaces;
using Rent.DTO;
using Rent.Models;
using Rent.Data;

namespace Rent.Services
{
 public class OrderService : IOrderService
 {
 private readonly DataContext? _db;
 private readonly OrderSqlService? _sql;
 private readonly ILogger<OrderService>? _logger;

 // Parameterless constructor left for unit tests that use new OrderService()
 public OrderService()
 {
 }

 // DI constructor used by the application
 public OrderService(DataContext db, OrderSqlService sql, ILogger<OrderService> logger)
 {
 _db = db;
 _sql = sql;
 _logger = logger;
 }

 // Existing synchronous method used by tests via IOrderService
 public Order CreateOrder(CreateOrderDto dto, string userId)
 {
 return new Order
 {
 Id =1,
 UserId = userId,
 BasePrice = dto.BasePrice,
 Days = dto.Days,
 Price = dto.BasePrice * dto.Days,
 Rented_Items = "auto",
 Date_Of_submission = System.DateOnly.FromDateTime(System.DateTime.UtcNow),
 Was_It_Returned = false
 };
 }

 // New async creation used by OrdersController when DI is available
 public async Task<(Order?, decimal)> CreateOrderAsync(CreateOrderDto dto, string userId)
 {
 // If DI services not available, fall back to simple in-memory behavior
 if (_db == null || _sql == null)
 {
 var o = CreateOrder(dto, userId);
 return (o, o.Price);
 }

 // Ensure counts
 if (dto.Days <=0) dto.Days =1;
 if (dto.BasePrice <0) dto.BasePrice =0;
 if (dto.ItemsCount <=0) dto.ItemsCount = System.Math.Max(0, dto.ItemsDetail?.Sum(i => i.Quantity) ??0);

 // create order via stored procedure
 var rentedItems = (dto.Items != null && dto.Items.Length >0)
 ? string.Join(", ", dto.Items)
 : "Basket";
 await _sql.ExecuteCreateOrderAsync(userId, rentedItems, dto.BasePrice, dto.ItemsCount, dto.Days);

 // load created order
 var order = await _db.Orders.Where(o => o.UserId == userId).OrderByDescending(o => o.Id).FirstOrDefaultAsync();
 if (order == null)
 {
 _logger?.LogError("spCreateOrder did not create an order for user {UserId}", userId);
 return (null,0m);
 }

 // ensure defaults
 if (string.IsNullOrEmpty(order.UserId)) order.UserId = userId;
 if (order.OrderDate == default) order.OrderDate = System.DateTime.UtcNow;
 if (order.Date_Of_submission == default) order.Date_Of_submission = System.DateOnly.FromDateTime(System.DateTime.UtcNow);
 order.Was_It_Returned = false;
 if ((order.Days ??0) <=0) order.Days = dto.Days;

 // assign equipment (do not reserve)
 foreach (var d in dto.ItemsDetail ?? new List<ItemDetailDto>())
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
 var provided = (d.EquipmentIds ?? new List<int>());

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
 PriceWhenOrdered = eq.Price
 });
 }
 }

 await _db.SaveChangesAsync();

 var total = await _sql.GetOrderTotalAsync(order.Id);

 order.BasePrice = dto.BasePrice;
 order.ItemsCount = dto.ItemsCount;
 order.Days = dto.Days;
 order.Price = total;
 order.DueDate = null;

 _db.Orders.Update(order);
 await _db.SaveChangesAsync();

 _logger?.LogInformation("Order created: Id={OrderId}, Final={Final}, BasePrice={BasePrice}, ItemsCount={ItemsCount}, Days={Days}",
 order.Id, total, order.BasePrice, order.ItemsCount, order.Days);

 return (order, total);
 }
 }
}