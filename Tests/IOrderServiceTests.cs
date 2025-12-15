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
 public void Setup()
 {
 var sc = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
 sc.AddTransient<IOrderService, OrderService>();
 var sp = sc.BuildServiceProvider();
 _svc = sp.GetRequiredService<IOrderService>();
 }

 [Test]
 public void Order_service_creates_order()
 {
 var dto = new CreateOrderDto { BasePrice =10m, Days =2 };
 var o = _svc.CreateOrder(dto, "user1");
 Assert.AreEqual("user1", o.UserId);
 Assert.AreEqual(20m, o.Price);
 }
 }
}
