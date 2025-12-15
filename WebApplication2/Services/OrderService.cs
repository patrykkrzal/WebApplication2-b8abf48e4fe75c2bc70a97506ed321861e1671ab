using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Rent.Interfaces;
using Rent.DTO;
using Rent.Models;
using Rent.Data;
using Rent.Services;

namespace Rent.Services
{
 public class OrderService : IOrderService
 {
 private readonly DataContext? _db;
 private readonly Rent.Interfaces.IOrderSqlService? _sql;
 private readonly Rent.Interfaces.IEquipmentStateService? _equipmentState;
 private readonly ILogger<OrderService>? _logger;
 private readonly Rent.Interfaces.IPriceResolver? _priceResolver;

 // ctor (tests)
 public OrderService()
 {
 }

 // ctor (di)
 public OrderService(DataContext db, Rent.Interfaces.IOrderSqlService sql, Rent.Interfaces.IEquipmentStateService equipmentState, ILogger<OrderService> logger, Rent.Interfaces.IPriceResolver priceResolver)
 {
 _db = db;
 _sql = sql;
 _equipmentState = equipmentState;
 _logger = logger;
 _priceResolver = priceResolver;
 }

 // create order (sync)
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

 // create order (async)
 public async Task<(Order?, decimal)> CreateOrderAsync(CreateOrderDto dto, string userId)
 {
 // If DI services not available, fall back to simple in-memory behavior
 if (_db == null || _sql == null)
 {
 var o = CreateOrder(dto, userId);
 return (o, o.Price);
 }

 // ensure counts
 if (dto.Days <=0) dto.Days =1;
 if (dto.BasePrice <0) dto.BasePrice =0;
 if (dto.ItemsCount <=0) dto.ItemsCount = System.Math.Max(0, dto.ItemsDetail?.Sum(i => i.Quantity) ??0);

 // create via sp
 var rentedItems = (dto.Items != null && dto.Items.Length >0)
 ? string.Join(", ", dto.Items)
 : "Basket";
 await _sql.ExecuteCreateOrderAsync(userId, rentedItems, dto.BasePrice, dto.ItemsCount, dto.Days);

 // load order
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

 // assign equipment
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
 PriceWhenOrdered = (eq.Price ?? (_priceResolver != null ? _priceResolver.ResolvePrice(eq.Type, eq.Size) :0m))
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

 // accept order
 public async Task<(bool Success, System.Collections.Generic.List<int> Reserved, System.DateTime? DueDate, string? Error)> AcceptAsync(int id)
 {
 if (_db == null) return (false, new System.Collections.Generic.List<int>(), null, "DB not available");
 var ord = await _db.Orders.Full().FirstOrDefaultAsync(o => o.Id == id);
 if (ord == null) return (false, new System.Collections.Generic.List<int>(), null, "Order not found");
 if (ord.DueDate != null) return (false, new System.Collections.Generic.List<int>(), null, "Order already accepted");
 if ((ord.Days ??0) <=0) ord.Days =7;
 ord.OrderDate = System.DateTime.UtcNow;
 ord.Was_It_Returned = false;

 var reserved = new System.Collections.Generic.List<int>();
 foreach (var oi in ord.OrderedItems ?? System.Linq.Enumerable.Empty<OrderedItem>())
 {
 if (oi.Equipment != null)
 {
 _equipmentState?.Reserve(oi);
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
 var refreshed = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);
 return (true, reserved, refreshed?.DueDate, null);
 }

 // return order
 public async Task<(bool Success, System.Collections.Generic.List<int> Restored, string? Error)> ReturnAsync(int id)
 {
 if (_db == null) return (false, new System.Collections.Generic.List<int>(), "DB not available");
 var ord = await _db.Orders.Full().FirstOrDefaultAsync(o => o.Id == id);
 if (ord == null) return (false, new System.Collections.Generic.List<int>(), "Order not found");
 if (ord.Was_It_Returned) return (false, new System.Collections.Generic.List<int>(), "Order already returned");

 ord.Was_It_Returned = true;
 ord.DueDate = null;

 var restored = new System.Collections.Generic.List<int>();
 foreach (var oi in ord.OrderedItems ?? System.Linq.Enumerable.Empty<OrderedItem>())
 {
 if (oi.Equipment != null)
 {
 _equipmentState?.Restore(oi);
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
 return (true, restored, null);
 }
 }
}