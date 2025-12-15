using NUnit.Framework;
using Rent.Interfaces;
using Rent.Services;
using Rent.Models;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

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
 var sc = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
 sc.AddTransient<IAvailabilityService>(sp => new AvailabilityService(_list));
 var sp = sc.BuildServiceProvider();
 _svc = sp.GetRequiredService<IAvailabilityService>();
 }

 [Test]
 public void AvailabilityOnlyAvailable()
 {
 var available = _svc.GetAvailableEquipment().ToList();
 Assert.AreEqual(1, available.Count);
 Assert.AreEqual(1, available[0].Id);
 }
 }
}
