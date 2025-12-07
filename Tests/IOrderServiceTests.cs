using NUnit.Framework;
using Rent.Interfaces;
using Rent.Services;
using Rent.DTO;

namespace Tests
{
 public class IOrderServiceTests
 {
 private IOrderService _svc = null!;
 [SetUp]
 public void Setup() => _svc = new OrderService();

 [Test]
 public void Order_service_creates_order()
 {
 var dto = new CreateOrderDto { BasePrice =10m, Days =2 };
 var o = _svc.CreateOrder(dto, "user1");
 Assert.AreEqual("user1", o.UserId);
 Assert.AreEqual(20m, o.Price);
 }

 [Test]
 public void Order_service_price_matches_expected()
 {
 var dto = new CreateOrderDto { BasePrice =5m, Days =3 };
 var o = _svc.CreateOrder(dto, "user2");
 Assert.AreEqual(15m, o.Price);
 Assert.AreEqual("user2", o.UserId);
 }

 [Test]
 public void Order_service_rented_items_default()
 {
 var dto = new CreateOrderDto { BasePrice =7m, Days =1 };
 var o = _svc.CreateOrder(dto, "u3");
 Assert.AreEqual("auto", o.Rented_Items);
 }
 }
}
