using Microsoft.AspNetCore.Mvc;
using Rent.Data;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using WebApplication2.DTO;
using Rent.Enums;

namespace Rent.Controllers
{
 [ApiController]
 [Route("api/equipment/prices")]
 public class PriceController : ControllerBase
 {
 private readonly DataContext db;
 private readonly IMemoryCache cache;
 private const string CachePrefix = "price_";
 public PriceController(DataContext db, IMemoryCache cache) { this.db = db; this.cache = cache; }

 [HttpGet]
 public IActionResult GetAll() => Ok(db.EquipmentPrices.Select(p => new { Type = p.Type.ToString(), Size = p.Size.ToString(), p.Price, p.Note }).ToList());

 [Authorize(Roles = "Admin,Worker")]
 [HttpPost]
 public IActionResult Upsert([FromBody] PriceUpsertDTO dto)
 {
 if (dto == null) return BadRequest("No payload");
 if (string.IsNullOrWhiteSpace(dto.Type) || string.IsNullOrWhiteSpace(dto.Size) || !dto.Price.HasValue) return BadRequest("Type, Size and Price are required");
 if (!System.Enum.TryParse<EquipmentType>(dto.Type, true, out var et)) return BadRequest("Invalid Type");
 if (!System.Enum.TryParse<Rent.Enums.Size>(dto.Size, true, out var sz)) return BadRequest("Invalid Size");

 var existing = db.EquipmentPrices.FirstOrDefault(x => x.Type == et && x.Size == sz);
 if (existing == null) db.EquipmentPrices.Add(new Rent.Models.EquipmentPrice { Type = et, Size = sz, Price = dto.Price.Value, Note = dto.Note });
 else { existing.Price = dto.Price.Value; existing.Note = dto.Note; }
 db.SaveChanges();

 var key = CachePrefix + ((int)et) + "_" + ((int)sz);
 try { cache.Remove(key); } catch { }

 return Ok(new { Type = et.ToString(), Size = sz.ToString(), Price = dto.Price.Value });
 }

 [Authorize(Roles = "Admin,Worker")]
 [HttpDelete]
 public IActionResult Delete([FromBody] PriceUpsertDTO dto)
 {
 if (dto == null) return BadRequest("No payload");
 if (string.IsNullOrWhiteSpace(dto.Type) || string.IsNullOrWhiteSpace(dto.Size)) return BadRequest("Type and Size required");
 if (!System.Enum.TryParse<EquipmentType>(dto.Type, true, out var et)) return BadRequest("Invalid Type");
 if (!System.Enum.TryParse<Rent.Enums.Size>(dto.Size, true, out var sz)) return BadRequest("Invalid Size");
 var existing = db.EquipmentPrices.FirstOrDefault(x => x.Type == et && x.Size == sz);
 if (existing == null) return NotFound();
 db.EquipmentPrices.Remove(existing); db.SaveChanges();
 var key = CachePrefix + ((int)et) + "_" + ((int)sz);
 try { cache.Remove(key); } catch { }
 return NoContent();
 }
 }
}
