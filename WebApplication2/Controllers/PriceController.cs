using Microsoft.AspNetCore.Mvc;
using Rent.Data;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using WebApplication2.DTO;
using Rent.Models;

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
 public IActionResult GetAll() => Ok(db.EquipmentPrices.Select(p => new { Type = p.Type, Size = p.Size, p.Price, p.Note }).ToList());

 [Authorize(Roles = "Admin,Worker")]
 [HttpPost]
 public IActionResult Upsert([FromBody] PriceUpsertDTO dto)
 {
 if (dto == null) return BadRequest("No payload");
 if (string.IsNullOrWhiteSpace(dto.Type) || string.IsNullOrWhiteSpace(dto.Size) || !dto.Price.HasValue) return BadRequest("Type, Size and Price are required");

 var typeNorm = dto.Type.Trim();
 var sizeNorm = dto.Size.Trim();

 var existing = db.EquipmentPrices.FirstOrDefault(x => x.Type.ToLower() == typeNorm.ToLower() && x.Size.ToLower() == sizeNorm.ToLower());
 if (existing == null) db.EquipmentPrices.Add(new Rent.Models.EquipmentPrice { Type = typeNorm, Size = sizeNorm, Price = dto.Price.Value, Note = dto.Note });
 else { existing.Price = dto.Price.Value; existing.Note = dto.Note; }
 db.SaveChanges();

 // Update existing physical equipment items to reflect new global price
 try
 {
 var itemsToUpdate = db.Equipment.Where(e => e.Type.ToLower() == typeNorm.ToLower() && e.Size.ToLower() == sizeNorm.ToLower()).ToList();
 if (itemsToUpdate.Any())
 {
 foreach (var item in itemsToUpdate)
 {
 item.Price = dto.Price.Value;
 }
 db.SaveChanges();
 }
 else
 {
 // If there are no physical items for this type/size, create one so availability and management UI show the size
 var created = new Equipment
 {
 Type = typeNorm,
 Size = sizeNorm,
 Is_In_Werehouse = true,
 Is_Reserved = false,
 Price = dto.Price.Value
 };
 db.Equipment.Add(created);
 db.SaveChanges();
 }
 }
 catch
 {
 // ignore update failures
 }

 var key = CachePrefix + typeNorm.ToLower() + "_" + sizeNorm.ToLower();
 try { cache.Remove(key); } catch { }

 return Ok(new { Type = typeNorm, Size = sizeNorm, Price = dto.Price.Value });
 }

 [Authorize(Roles = "Admin,Worker")]
 [HttpDelete]
 public IActionResult Delete([FromBody] PriceUpsertDTO dto)
 {
 if (dto == null) return BadRequest("No payload");
 if (string.IsNullOrWhiteSpace(dto.Type) || string.IsNullOrWhiteSpace(dto.Size)) return BadRequest("Type and Size required");

 var typeNorm = dto.Type.Trim();
 var sizeNorm = dto.Size.Trim();

 var existing = db.EquipmentPrices.FirstOrDefault(x => x.Type.ToLower() == typeNorm.ToLower() && x.Size.ToLower() == sizeNorm.ToLower());
 if (existing == null) return NotFound();
 db.EquipmentPrices.Remove(existing); db.SaveChanges();
 // remove any placeholder Equipment items that were auto-created during Upsert and are not reserved and in warehouse
 try
 {
 var placeholders = db.Equipment.Where(e => e.Type.ToLower() == typeNorm.ToLower() && e.Size.ToLower() == sizeNorm.ToLower() && e.Is_In_Werehouse && !e.Is_Reserved).ToList();
 if (placeholders != null && placeholders.Any())
 {
 db.Equipment.RemoveRange(placeholders);
 db.SaveChanges();
 }
 }
 catch { /* ignore cleanup errors */ }
 var key = CachePrefix + typeNorm.ToLower() + "_" + sizeNorm.ToLower();
 try { cache.Remove(key); } catch { }
 return NoContent();
 }

 // Remove an offering entirely: delete unreserved equipment entries and the price entry.
 [Authorize(Roles = "Admin,Worker")]
 [HttpPost("remove-offer")]
 public IActionResult RemoveOffer([FromBody] PriceUpsertDTO dto)
 {
 if (dto == null) return BadRequest("No payload");
 if (string.IsNullOrWhiteSpace(dto.Type) || string.IsNullOrWhiteSpace(dto.Size)) return BadRequest("Type and Size required");

 var typeNorm = dto.Type.Trim();
 var sizeNorm = dto.Size.Trim();

 int deletedEquip =0;
 bool priceDeleted = false;
 try
 {
 var toDelete = db.Equipment.Where(e => e.Type.ToLower() == typeNorm.ToLower() && e.Size.ToLower() == sizeNorm.ToLower() && !e.Is_Reserved).ToList();
 if (toDelete.Any())
 {
 deletedEquip = toDelete.Count;
 db.Equipment.RemoveRange(toDelete);
 db.SaveChanges();
 }

 var existing = db.EquipmentPrices.FirstOrDefault(x => x.Type.ToLower() == typeNorm.ToLower() && x.Size.ToLower() == sizeNorm.ToLower());
 if (existing != null)
 {
 db.EquipmentPrices.Remove(existing);
 db.SaveChanges();
 priceDeleted = true;
 }
 }
 catch { /* ignore errors */ }

 var key = CachePrefix + typeNorm.ToLower() + "_" + sizeNorm.ToLower();
 try { cache.Remove(key); } catch { }

 return Ok(new { DeletedEquipment = deletedEquip, PriceDeleted = priceDeleted });
 }
 }
}
