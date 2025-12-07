using NUnit.Framework;
using Rent.Services;
using Rent.Data;
using Rent.Models;
using Rent.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Tests
{
 public class IPriceResolverTests
 {
 private IPriceResolver _resolver = null!;
 private DataContext _db = null!;
 [SetUp]
 public void Setup()
 {
 var opts = new DbContextOptionsBuilder<DataContext>().UseInMemoryDatabase("pricetest").Options;
 _db = new DataContext(opts);
 _db.EquipmentPrices.Add(new EquipmentPrice { Id =1, Type = EquipmentType.Skis, Size = Size.Small, Price =111m });
 _db.SaveChanges();
 _resolver = new EfPriceResolver(_db, new MemoryCache(new MemoryCacheOptions()));
 }

 [Test]
 public void Price_resolver_returns_db_price()
 {
 var p = _resolver.ResolvePrice(EquipmentType.Skis, Size.Small);
 Assert.AreEqual(111m, p);
 }
 }
}
