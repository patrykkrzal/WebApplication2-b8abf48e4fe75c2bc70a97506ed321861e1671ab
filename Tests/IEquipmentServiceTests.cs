using NUnit.Framework;
using Rent.Interfaces;
using Rent.Services;
using Rent.DTO;
using Rent.Models;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Tests
{
    public class IEquipmentServiceTests
    {
        private IEquipmentService _svc = null!;
        [SetUp]
        public void Setup()
        {
            var sc = new ServiceCollection();
            sc.AddTransient<IEquipmentService, EquipmentService>();
            var sp = sc.BuildServiceProvider();
            _svc = sp.GetRequiredService<IEquipmentService>();
        }

        [Test]
        public void EquipmentAddsAndReturnsAll()
        {
            var dto = new CreateEquipmentDTO { Type = "Skis", Size = "Medium", Price =10m };
            var e = _svc.AddEquipment(dto);
            var all = _svc.GetAll().ToList();
            Assert.AreEqual(1, all.Count);
            Assert.AreEqual(e.Id, all[0].Id);
        }
    }
}
