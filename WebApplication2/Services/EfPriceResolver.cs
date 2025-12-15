using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using Rent.Data;
using Rent.Models;
using Rent.Interfaces;

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

 public decimal ResolvePrice(string type, string size)
 {
 var tnorm = (type ?? "").ToLower();
 var snorm = (size ?? "").ToLower();
 var key = CachePrefix + tnorm + "_" + snorm;
 if (cache.TryGetValue<decimal>(key, out var cached)) return cached;

 var ep = db.Set<EquipmentPrice>().FirstOrDefault(p => p.Type.ToLower() == tnorm && p.Size.ToLower() == snorm);
 if (ep != null)
 {
 cache.Set(key, ep.Price, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = System.TimeSpan.FromMinutes(30) });
 return ep.Price;
 }


 decimal price =0m;
 var t = tnorm;
 var s = snorm;
 if (t.Contains("skis") || t.Contains("narty") || t.Contains("ski"))
 {
 if (s.Contains("small")) price =120m;
 else if (s.Contains("medium")) price =130m;
 else if (s.Contains("large")) price =140m;
 else price =130m;
 }
 else if (t.Contains("helmet")) price =35m;
 else if (t.Contains("gloves")) price =15m;
 else if (t.Contains("poles")) price =22m;
 else if (t.Contains("snowboard")) price =160m;
 else if (t.Contains("goggles")) price =55m;

 cache.Set(key, price, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = System.TimeSpan.FromMinutes(10) });
 return price;
 }
 }
}