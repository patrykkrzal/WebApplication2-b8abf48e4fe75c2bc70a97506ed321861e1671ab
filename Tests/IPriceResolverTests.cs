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
 // use a unique in-memory database name for each test run to avoid duplicate key issues
 var opts = new DbContextOptionsBuilder<DataContext>().UseInMemoryDatabase(System.Guid.NewGuid().ToString()).Options;
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

 [Test]
 public void Price_resolver_falls_back_to_default_if_missing()
 {
 var p = _resolver.ResolvePrice(EquipmentType.Goggles, Size.Universal);
 // default mapper in EfPriceResolver returns some fallback number (should be non-negative)
 Assert.GreaterOrEqual(p,0m);
 }
 }
}
