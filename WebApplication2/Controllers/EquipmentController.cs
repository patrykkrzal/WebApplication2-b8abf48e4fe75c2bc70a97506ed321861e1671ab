using Microsoft.AspNetCore.Mvc;
using Rent.Data;
using Rent.DTO;
using Rent.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore; 
using Microsoft.AspNetCore.Authorization;
using Rent.Services;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;
using System;
using System.Collections.Generic;

namespace Rent.Controllers
{
 [ApiController]
 [Route("api/equipment")]
 public class EquipmentController : ControllerBase
 {
 private readonly DataContext dbContext;
 private readonly IPriceResolver priceResolver;
 private readonly IWebHostEnvironment env;

 public EquipmentController(DataContext dbContext, IPriceResolver priceResolver, IWebHostEnvironment env)
 {
 this.dbContext = dbContext;
 this.priceResolver = priceResolver;
 this.env = env;
 }

 [HttpGet]
 public IActionResult GetAllEquipment()
 {
 var equipments = dbContext.Equipment.AsNoTracking().ToList();

 var allEquipment = equipments
 .Select(e => new {
 e.Id,
 Type = e.Type,
 Size = e.Size,
 e.Is_In_Werehouse,
 e.Is_Reserved,
 UnitPrice = priceResolver.ResolvePrice(e.Type, e.Size),
 // include original Price for admin scenarios
 Price = e.Price,
 ImageUrl = e.ImageUrl,
 e.RentalInfoId
 })
 .ToList();
 return Ok(allEquipment);
 }

 [HttpGet("{id:int}")]
 public IActionResult GetById(int id)
 {
 var eq = dbContext.Equipment.Find(id);
 if (eq is null) return NotFound();
 var dto = new {
 eq.Id,
 Type = eq.Type,
 Size = eq.Size,
 eq.Is_In_Werehouse,
 eq.Is_Reserved,
 UnitPrice = priceResolver.ResolvePrice(eq.Type, eq.Size),
 Price = eq.Price,
 ImageUrl = eq.ImageUrl,
 eq.RentalInfoId
 };
 return Ok(dto);
 }

 [HttpGet("availability")]
 public IActionResult GetAvailability()
 {
 var groupedCounts = dbContext.Equipment
 .AsNoTracking()
 .Where(e => e.Is_In_Werehouse && !e.Is_Reserved)
 .GroupBy(e => new { Type = e.Type, Size = e.Size })
 .Select(g => new
 {
 Type = g.Key.Type,
 Size = g.Key.Size,
 Count = g.Count(),
 SampleImage = g.Select(x => x.ImageUrl).FirstOrDefault()
 })
 .ToList();

 var grouped = groupedCounts
 .Select(g => new
 {
 Type = g.Type,
 Size = g.Size,
 Count = g.Count,
 UnitPrice = priceResolver.ResolvePrice(g.Type, g.Size),
 ImageUrl = g.SampleImage
 })
 .ToList();

 return Ok(grouped);
 }

 [Authorize(Roles = "Admin,Worker")]
 [HttpPost("add")] 
 public IActionResult AddEquipment([FromBody] JsonElement body)
 {
 if (body.ValueKind == JsonValueKind.Undefined || body.ValueKind == JsonValueKind.Null)
 return BadRequest(new { Message = "Wybierz poprawny typ i rozmiar." });

 string? typeStr = null;
 string? sizeStr = null;
 string? imageUrl = null;
 decimal? price = null;
 int? qty = null;

 if (body.TryGetProperty("Type", out var pType))
 {
 if (pType.ValueKind == JsonValueKind.String) typeStr = pType.GetString();
 else typeStr = pType.ToString();
 }
 if (body.TryGetProperty("Size", out var pSize))
 {
 if (pSize.ValueKind == JsonValueKind.String) sizeStr = pSize.GetString();
 else sizeStr = pSize.ToString();
 }
 if (body.TryGetProperty("ImageUrl", out var pImg))
 {
 if (pImg.ValueKind == JsonValueKind.String) imageUrl = pImg.GetString();
 else imageUrl = pImg.ToString();
 }
 if (body.TryGetProperty("Price", out var pPrice))
 {
 if (pPrice.ValueKind == JsonValueKind.Number && pPrice.TryGetDecimal(out var dp)) price = dp;
 else
 {
 var s = pPrice.ValueKind == JsonValueKind.String ? pPrice.GetString() : null;
 if (!string.IsNullOrWhiteSpace(s) && decimal.TryParse(s, out var dp2)) price = dp2;
 }
 }
 if (body.TryGetProperty("Quantity", out var pQty))
 {
 if (pQty.ValueKind == JsonValueKind.Number && pQty.TryGetInt32(out var iq)) qty = iq;
 else
 {
 var s = pQty.ValueKind == JsonValueKind.String ? pQty.GetString() : null;
 if (!string.IsNullOrWhiteSpace(s) && int.TryParse(s, out var iq2)) qty = iq2;
 }
 }

 if (string.IsNullOrWhiteSpace(typeStr) || string.IsNullOrWhiteSpace(sizeStr))
 return BadRequest(new { Message = "Wybierz poprawny typ i rozmiar." });

 try
 {
 var finalPrice = price ?? priceResolver.ResolvePrice(typeStr, sizeStr);
 var quantity = qty ??1;

 var created = new List<Equipment>();
 var spFailed = false;
 if (dbContext.Database.IsSqlServer())
 {
 var conn = (SqlConnection)dbContext.Database.GetDbConnection();
 if (conn.State != System.Data.ConnectionState.Open) conn.Open();

 for (int i =0; i < quantity; i++)
 {
 try
 {
 using var cmd = new SqlCommand("dbo.spAddEquipment", conn) { CommandType = System.Data.CommandType.StoredProcedure };
 // try to pass textual values if SP supports it
 cmd.Parameters.Add(new SqlParameter("@TypeName", System.Data.SqlDbType.NVarChar,100) { Value = typeStr });
 cmd.Parameters.Add(new SqlParameter("@SizeName", System.Data.SqlDbType.NVarChar,50) { Value = sizeStr });
 cmd.Parameters.Add(new SqlParameter("@Price", System.Data.SqlDbType.Decimal) { Precision =18, Scale =2, Value = finalPrice });
 // optional image param (SP may ignore it)
 cmd.Parameters.Add(new SqlParameter("@ImageUrl", System.Data.SqlDbType.NVarChar,500) { Value = (object?)imageUrl ?? DBNull.Value });

 var obj = cmd.ExecuteScalar();
 int newId =0;
 if (obj != null && obj != DBNull.Value)
 {
 if (int.TryParse(obj.ToString(), out var parsed)) newId = parsed;
 }
 created.Add(new Equipment { Id = newId, Type = typeStr, Size = sizeStr, Is_In_Werehouse = true, Is_Reserved = false, Price = finalPrice, ImageUrl = imageUrl });
 }
 catch (SqlException sqlEx)
 {
 // If SP signature doesn't match, fall back to EF and mark spFailed
 spFailed = true;
 _ = sqlEx; // keep variable available for debugging if needed
 // create via EF instead of SP for this item
 var item = new Equipment
 {
 Type = typeStr,
 Size = sizeStr,
 Is_In_Werehouse = true,
 Is_Reserved = false,
 Price = finalPrice,
 ImageUrl = imageUrl
 };
 dbContext.Equipment.Add(item);
 dbContext.SaveChanges();
 created.Add(item);
 }
 }

 // if SP failed for one or more items, continue but notify client
 if (spFailed)
 {
 return Ok(new { Message = "Equipment added (some via EF fallback because DB SP did not accept textual params)", Type = typeStr, Size = sizeStr, Price = finalPrice, Quantity = quantity, CreatedIds = created.Select(c => c.Id).ToArray() });
 }

 return Ok(new { Message = "Equipment added (via SP)", Type = typeStr, Size = sizeStr, Price = finalPrice, Quantity = quantity, CreatedIds = created.Select(c => c.Id).ToArray() });
 }

 for (int i =0; i < quantity; i++)
 {
 var item = new Equipment
 {
 Type = typeStr,
 Size = sizeStr,
 Is_In_Werehouse = true,
 Is_Reserved = false,
 Price = finalPrice,
 ImageUrl = imageUrl
 };
 dbContext.Equipment.Add(item);
 created.Add(item);
 }
 dbContext.SaveChanges();

 return Ok(new { Message = "Equipment added", Type = typeStr, Size = sizeStr, Price = finalPrice, Quantity = quantity, CreatedIds = created.Select(c => c.Id).ToArray() });
 }
 catch (System.Exception ex)
 {
 return StatusCode(500, new { Message = "Failed to add equipment", Error = ex.Message });
 }
 }

 [Authorize(Roles = "Admin,Worker")]
 [HttpDelete("delete")]
 public IActionResult DeleteOne([FromBody] JsonElement body)
 {
 if (body.ValueKind == JsonValueKind.Undefined || body.ValueKind == JsonValueKind.Null)
 return BadRequest(new { Message = "Empty payload" });

 string? typeStr = null;
 string? sizeStr = null;
 int? qty = null;

 if (body.TryGetProperty("Type", out var pType))
 {
 if (pType.ValueKind == JsonValueKind.String) typeStr = pType.GetString();
 else typeStr = pType.ToString();
 }
 if (body.TryGetProperty("Size", out var pSize))
 {
 if (pSize.ValueKind == JsonValueKind.String) sizeStr = pSize.GetString();
 else sizeStr = pSize.ToString();
 }
 if (body.TryGetProperty("Quantity", out var pQty))
 {
 if (pQty.ValueKind == JsonValueKind.Number && pQty.TryGetInt32(out var iq)) qty = iq;
 else
 {
 var s = pQty.ValueKind == JsonValueKind.String ? pQty.GetString() : null;
 if (!string.IsNullOrWhiteSpace(s) && int.TryParse(s, out var iq2)) qty = iq2;
 }
 }

 if (string.IsNullOrWhiteSpace(typeStr) || string.IsNullOrWhiteSpace(sizeStr))
 return BadRequest(new { Message = "Wybierz poprawny typ i rozmiar." });

 try
 {
 var quantity = qty ??1;
 var toDelete = dbContext.Equipment
 .Where(e => e.Type == typeStr && e.Size == sizeStr && e.Is_In_Werehouse && !e.Is_Reserved)
 .Take(quantity)
 .ToList();
 int deleted = toDelete.Count;
 if (deleted >0)
 {
 dbContext.Equipment.RemoveRange(toDelete);
 dbContext.SaveChanges();
 }
 var remaining = dbContext.Equipment.Count(e => e.Type == typeStr && e.Size == sizeStr && e.Is_In_Werehouse && !e.Is_Reserved);
 return Ok(new { Message = "Deleted equipment items", Type = typeStr, Size = sizeStr, Deleted = deleted, Remaining = remaining });
 }
 catch (System.Exception ex)
 {
 return StatusCode(500, new { Message = "Failed to delete equipment", Error = ex.Message });
 }
 }

 [Authorize(Roles = "Admin,Worker")]
 [HttpDelete("{id:int}")]
 public IActionResult DeleteById(int id)
 {
 var entity = dbContext.Equipment.Find(id);
 if (entity is null) return NotFound();
 dbContext.Equipment.Remove(entity);
 dbContext.SaveChanges();
 return NoContent();
 }

 [Authorize(Roles = "Admin,Worker")]
 [HttpPut("{id:int}")]
 public IActionResult Update(int id, [FromBody] UpdateEquipmentDTO dto)
 {
 var entity = dbContext.Equipment.Find(id);
 if (entity is null) return NotFound();
 if (dto.Price.HasValue) entity.Price = dto.Price.Value;
 if (dto.Is_In_Werehouse.HasValue) entity.Is_In_Werehouse = dto.Is_In_Werehouse.Value;
 if (dto.Is_Reserved.HasValue) entity.Is_Reserved = dto.Is_Reserved.Value;
 dbContext.SaveChanges();
 return Ok(entity);
 }

 [Authorize(Roles = "Admin,Worker")]
 [HttpPost("upload")]
 public async Task<IActionResult> UploadImage([FromForm] IFormFile file)
 {
 if (file == null) return BadRequest(new { Message = "No file provided" });
 if (file.Length ==0) return BadRequest(new { Message = "Empty file" });
 var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
 var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
 if (!allowed.Contains(ext)) return BadRequest(new { Message = "Invalid file type" });

 try
 {
 var uploads = Path.Combine(env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
 if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);
 var fileName = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "_" + Guid.NewGuid().ToString("N") + ext;
 var filePath = Path.Combine(uploads, fileName);
 using (var fs = System.IO.File.Create(filePath))
 {
 await file.CopyToAsync(fs);
 }
 var publicUrl = Path.Combine("/uploads", fileName).Replace("\\","/");

 // Do NOT modify existing Equipment records here — return URL only.
 return Ok(new { Url = publicUrl });
 }
 catch (Exception ex)
 {
 return StatusCode(500, new { Message = "Upload failed", Error = ex.Message });
 }
 }
 }
}
