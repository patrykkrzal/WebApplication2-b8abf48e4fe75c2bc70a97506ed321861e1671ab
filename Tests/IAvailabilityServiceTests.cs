using NUnit.Framework;
using Rent.Interfaces;
using Rent.Services;
using Rent.Models;
using System.Collections.Generic;
using System.Linq;

namespace Tests
{
 public class IAvailabilityServiceTests
 {
 private IAvailabilityService _svc = null!;
 private List<Equipment> _list = null!;

 [SetUp]
 public void Setup()
 {
 _list = new List<Equipment>
 {
 new Equipment { Id =1, Is_In_Werehouse = true, Is_Reserved = false },
 new Equipment { Id =2, Is_In_Werehouse = false, Is_Reserved = false },
 new Equipment { Id =3, Is_In_Werehouse = true, Is_Reserved = true }
 };
 _svc = new AvailabilityService(_list);
 }

 [Test]
 public void Availability_returns_only_available()
 {
 var available = _svc.GetAvailableEquipment().ToList();
 Assert.AreEqual(1, available.Count);
 Assert.AreEqual(1, available[0].Id);
 }

 [Test]
 public void Availability_enumeration_does_not_modify_source()
 {
 // enumerating should not remove items from original list
 var beforeCount = _list.Count;
 var a = _svc.GetAvailableEquipment().ToList();
 Assert.AreEqual(beforeCount, _list.Count);
 }

 [Test]
 public void Availability_allows_multiple_available_items()
 {
 // make second item available
 _list[1].Is_In_Werehouse = true;
 _list[1].Is_Reserved = false;
 var available = _svc.GetAvailableEquipment().OrderBy(e => e.Id).ToList();
 Assert.AreEqual(2, available.Count);
 Assert.AreEqual(new[] {1,2}, available.Select(x => x.Id).ToArray());
 }
 }
}
