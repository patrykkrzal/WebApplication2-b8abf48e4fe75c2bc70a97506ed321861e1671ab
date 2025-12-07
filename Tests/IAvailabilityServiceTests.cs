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
 [SetUp]
 public void Setup()
 {
 var list = new List<Equipment>
 {
 new Equipment { Id =1, Is_In_Werehouse = true, Is_Reserved = false },
 new Equipment { Id =2, Is_In_Werehouse = false, Is_Reserved = false },
 new Equipment { Id =3, Is_In_Werehouse = true, Is_Reserved = true }
 };
 _svc = new AvailabilityService(list);
 }

 [Test]
 public void Availability_returns_only_available()
 {
 var available = _svc.GetAvailableEquipment().ToList();
 Assert.AreEqual(1, available.Count);
 Assert.AreEqual(1, available[0].Id);
 }
 }
}
