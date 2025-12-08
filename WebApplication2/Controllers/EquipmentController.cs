using Microsoft.AspNetCore.Mvc;
using Rent.Data;
using Rent.DTO;
using Rent.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore; 
using Microsoft.AspNetCore.Authorization;
using Rent.Enums;
using Rent.Services;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace Rent.Controllers
{
    [ApiController]
    [Route("api/equipment")]
    public class EquipmentController : ControllerBase
    {
        private readonly DataContext dbContext;
        private readonly IPriceResolver priceResolver;

        public EquipmentController(DataContext dbContext, IPriceResolver priceResolver)
        {
            this.dbContext = dbContext;
            this.priceResolver = priceResolver;
        }

        [HttpGet]
        public IActionResult GetAllEquipment()
        {
            
            var equipments = dbContext.Equipment.AsNoTracking().ToList();

            var allEquipment = equipments
                .Select(e => new {
                    e.Id,
                    Type = e.Type.ToString(),
                    Size = e.Size.ToString(),
                    e.Is_In_Werehouse,
                    e.Is_Reserved,
                    UnitPrice = priceResolver.ResolvePrice(e.Type, e.Size),
                    // include original Price for admin scenarios
                    Price = e.Price,
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
                Type = eq.Type.ToString(),
                Size = eq.Size.ToString(),
                eq.Is_In_Werehouse,
                eq.Is_Reserved,
                UnitPrice = priceResolver.ResolvePrice(eq.Type, eq.Size),
                Price = eq.Price,
                eq.RentalInfoId
            };
            return Ok(dto);
        }

        [HttpGet("availability")]
        public IActionResult GetAvailability()
        {
            // materialize grouped counts first, then resolve prices to avoid nested readers
            var groupedCounts = dbContext.Equipment
                .AsNoTracking()
                .Where(e => e.Is_In_Werehouse && !e.Is_Reserved)
                .GroupBy(e => new { e.Type, e.Size })
                .Select(g => new
                {
                    Type = g.Key.Type,
                    Size = g.Key.Size,
                    Count = g.Count()
                })
                .ToList();

            var grouped = groupedCounts
                .Select(g => new
                {
                    Type = g.Type.ToString(),
                    Size = g.Size.ToString(),
                    Count = g.Count,
                    UnitPrice = priceResolver.ResolvePrice(g.Type, g.Size)
                })
                .ToList();

            return Ok(grouped);
        }

        [Authorize(Roles = "Admin,Worker")]
        [HttpPost("add")] 
        public IActionResult AddEquipment([FromBody] JsonElement body)
        {
            // Accept raw JSON and validate fields manually to provide friendly error messages
            if (body.ValueKind == JsonValueKind.Undefined || body.ValueKind == JsonValueKind.Null)
                return BadRequest(new { Message = "Wybierz poprawny typ i rozmiar." });

            string? typeStr = null;
            string? sizeStr = null;
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

            // Validate required fields
            if (string.IsNullOrWhiteSpace(typeStr) || string.IsNullOrWhiteSpace(sizeStr))
                return BadRequest(new { Message = "Wybierz poprawny typ i rozmiar." });

            if (!System.Enum.TryParse<EquipmentType>(typeStr.Trim(), true, out var et))
                return BadRequest(new { Message = "Wybierz poprawny typ i rozmiar." });

            if (!System.Enum.TryParse<Size>(sizeStr.Trim(), true, out var sz))
                return BadRequest(new { Message = "Wybierz poprawny typ i rozmiar." });

            // now we have validated inputs, proceed
            try
            {
                var finalPrice = price ?? priceResolver.ResolvePrice(et, sz);
                var quantity = qty ??1;

                var created = new List<Equipment>();
                if (dbContext.Database.IsSqlServer())
                {
                    var conn = (SqlConnection)dbContext.Database.GetDbConnection();
                    if (conn.State != System.Data.ConnectionState.Open) conn.Open();

                    for (int i =0; i < quantity; i++)
                    {
                        using var cmd = new SqlCommand("dbo.spAddEquipment", conn) { CommandType = System.Data.CommandType.StoredProcedure };
                        cmd.Parameters.Add(new SqlParameter("@Type", System.Data.SqlDbType.Int) { Value = (int)et });
                        cmd.Parameters.Add(new SqlParameter("@Size", System.Data.SqlDbType.Int) { Value = (int)sz });
                        cmd.Parameters.Add(new SqlParameter("@Price", System.Data.SqlDbType.Decimal) { Precision =18, Scale =2, Value = finalPrice });

                        var obj = cmd.ExecuteScalar();
                        int newId =0;
                        if (obj != null && obj != DBNull.Value)
                        {
                            if (int.TryParse(obj.ToString(), out var parsed)) newId = parsed;
                        }

                        created.Add(new Equipment { Id = newId, Type = et, Size = sz, Is_In_Werehouse = true, Is_Reserved = false, Price = finalPrice });
                    }

                    return Ok(new { Message = "Equipment added (via SP)", Type = et.ToString(), Size = sz.ToString(), Price = finalPrice, Quantity = quantity, CreatedIds = created.Select(c => c.Id).ToArray() });
                }

                for (int i =0; i < quantity; i++)
                {
                    var item = new Equipment
                    {
                        Type = et,
                        Size = sz,
                        Is_In_Werehouse = true,
                        Is_Reserved = false,
                        Price = finalPrice
                    };
                    dbContext.Equipment.Add(item);
                    created.Add(item);
                }
                dbContext.SaveChanges();

                return Ok(new { Message = "Equipment added", Type = et.ToString(), Size = sz.ToString(), Price = finalPrice, Quantity = quantity, CreatedIds = created.Select(c => c.Id).ToArray() });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to add equipment", Error = ex.Message });
            }
        }

        // DELETE one available 
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

            if (!System.Enum.TryParse<EquipmentType>(typeStr.Trim(), true, out var et))
                return BadRequest(new { Message = "Wybierz poprawny typ i rozmiar." });

            if (!System.Enum.TryParse<Size>(sizeStr.Trim(), true, out var sz))
                return BadRequest(new { Message = "Wybierz poprawny typ i rozmiar." });

            try
            {
                var quantity = qty ??1;
                var toDelete = dbContext.Equipment
                    .Where(e => e.Type == et && e.Size == sz && e.Is_In_Werehouse && !e.Is_Reserved)
                    .Take(quantity)
                    .ToList();
                int deleted = toDelete.Count;
                if (deleted >0)
                {
                    dbContext.Equipment.RemoveRange(toDelete);
                    dbContext.SaveChanges();
                }
                var remaining = dbContext.Equipment.Count(e => e.Type == et && e.Size == sz && e.Is_In_Werehouse && !e.Is_Reserved);
                return Ok(new { Message = "Deleted equipment items", Type = et.ToString(), Size = sz.ToString(), Deleted = deleted, Remaining = remaining });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to delete equipment", Error = ex.Message });
            }
        }

        [Authorize(Roles = "Admin,Worker")]
        [HttpDelete("{id:int}")] // delete by id
        public IActionResult DeleteById(int id)
        {
            var entity = dbContext.Equipment.Find(id);
            if (entity is null) return NotFound();
            dbContext.Equipment.Remove(entity);
            dbContext.SaveChanges();
            return NoContent();
        }

        [Authorize(Roles = "Admin,Worker")]
        [HttpPut("{id:int}")] // update price/flags
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
    }
}
