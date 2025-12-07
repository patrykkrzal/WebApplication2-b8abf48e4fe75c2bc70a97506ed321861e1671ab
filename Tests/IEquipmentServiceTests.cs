using NUnit.Framework;
using Rent.Interfaces;
using Rent.Services;
using Rent.DTO;
using Rent.Models;
using System.Linq;
using Rent.Enums;

namespace Tests
{
 public class IEquipmentServiceTests
 {
 private IEquipmentService _svc = null!;
 [SetUp]
 public void Setup() => _svc = new EquipmentService();

 [Test]
 public void Equipment_service_adds_and_returns_all()
 {
 var dto = new CreateEquipmentDTO { Type = EquipmentType.Skis, Size = Size.Medium, Price =10m };
 var e = _svc.AddEquipment(dto);
 var all = _svc.GetAll().ToList();
 Assert.AreEqual(1, all.Count);
 Assert.AreEqual(e.Id, all[0].Id);
 }

 [Test]
 public void Equipment_service_generates_unique_ids()
 {
 var dto1 = new CreateEquipmentDTO { Type = EquipmentType.Skis, Size = Size.Medium, Price =10m };
 var dto2 = new CreateEquipmentDTO { Type = EquipmentType.Helmet, Size = Size.Universal, Price =5m };
 var e1 = _svc.AddEquipment(dto1);
 var e2 = _svc.AddEquipment(dto2);
 Assert.AreNotEqual(e1.Id, e2.Id);
 }
 }
}
