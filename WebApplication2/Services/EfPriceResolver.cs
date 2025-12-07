using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using Rent.Data;
using Rent.Models;
using Rent.Enums;

namespace Rent.Services
{
 public class EfPriceResolver : IPriceResolver
 {
 private readonly DataContext db;
 private readonly IMemoryCache cache;
 private static readonly string CachePrefix = "price_";

 public EfPriceResolver(DataContext db, IMemoryCache cache)
 {
 this.db = db;
 this.cache = cache;
 }

 public decimal ResolvePrice(EquipmentType type, Size size)
 {
 var key = CachePrefix + (int)type + "_" + (int)size;
 if (cache.TryGetValue<decimal>(key, out var cached)) return cached;

 var ep = db.Set<EquipmentPrice>().FirstOrDefault(p => p.Type == type && p.Size == size);
 if (ep != null)
 {
 cache.Set(key, ep.Price, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = System.TimeSpan.FromMinutes(30) });
 return ep.Price;
 }

 // fallback to legacy defaults
 var price = type switch
 {
 EquipmentType.Skis => size switch { Size.Small =>120m, Size.Medium =>130m, Size.Large =>140m, _ =>130m },
 EquipmentType.Helmet =>35m,
 EquipmentType.Gloves =>15m,
 EquipmentType.Poles =>22m,
 EquipmentType.Snowboard =>160m,
 EquipmentType.Goggles =>55m,
 _ =>0m
 };
 cache.Set(key, price, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = System.TimeSpan.FromMinutes(10) });
 return price;
 }
 }
}