using Microsoft.AspNetCore.Mvc;
using Rent.Data;
using Rent.DTO;
using Rent.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore; // for Database facade
using Microsoft.AspNetCore.Authorization;
using Rent.Enums;
using Rent.Services;

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
            // Prevent nested DataReader by materializing equipment first
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
        [HttpPost("add")] // unique route to avoid Swagger conflicts
        public IActionResult AddEquipment([FromBody] CreateEquipmentDTO dto)
        {
            if (dto == null) return BadRequest(new { Message = "Empty payload" });
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Where(kv => kv.Value.Errors.Any())
                    .ToDictionary(kv => kv.Key, kv => kv.Value.Errors.Select(e => e.ErrorMessage).ToArray());
                return BadRequest(new { Message = "Invalid payload", Errors = errors });
            }

            try
            {
                var price = dto.Price ?? priceResolver.ResolvePrice(dto.Type, dto.Size);
                var qty = dto.Quantity ??1;

                // create items directly to avoid dependency on stored proc
                var created = new List<Equipment>();
                for (int i =0; i < qty; i++)
                {
                    var item = new Equipment
                    {
                        Type = dto.Type,
                        Size = dto.Size,
                        Is_In_Werehouse = true,
                        Is_Reserved = false,
                        Price = price
                    };
                    dbContext.Equipment.Add(item);
                    created.Add(item);
                }
                dbContext.SaveChanges();

                return Ok(new { Message = "Equipment added", Type = dto.Type.ToString(), Size = dto.Size.ToString(), Price = price, Quantity = qty, CreatedIds = created.Select(c => c.Id).ToArray() });
            }
            catch (System.Exception ex)
            {
                // return minimal error info to client for troubleshooting
                return StatusCode(500, new { Message = "Failed to add equipment", Error = ex.Message });
            }
        }

        // DELETE one available (not reserved, in warehouse) item for given type+size
        [Authorize(Roles = "Admin,Worker")]
        [HttpDelete("delete")]
        public IActionResult DeleteOne([FromBody] CreateEquipmentDTO dto)
        {
            if (dto == null) return BadRequest(new { Message = "Empty payload" });
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Where(kv => kv.Value.Errors.Any())
                    .ToDictionary(kv => kv.Key, kv => kv.Value.Errors.Select(e => e.ErrorMessage).ToArray());
                return BadRequest(new { Message = "Invalid payload", Errors = errors });
            }

            try
            {
                var qty = dto.Quantity ??1;
                // Fetch up to 'qty' available items and remove them in one batch
                var toDelete = dbContext.Equipment
                    .Where(e => e.Type == dto.Type && e.Size == dto.Size && e.Is_In_Werehouse && !e.Is_Reserved)
                    .Take(qty)
                    .ToList();
                int deleted = toDelete.Count;
                if (deleted >0)
                {
                    dbContext.Equipment.RemoveRange(toDelete);
                    dbContext.SaveChanges();
                }
                var remaining = dbContext.Equipment.Count(e => e.Type == dto.Type && e.Size == dto.Size && e.Is_In_Werehouse && !e.Is_Reserved);
                return Ok(new { Message = "Deleted equipment items", Type = dto.Type.ToString(), Size = dto.Size.ToString(), Deleted = deleted, Remaining = remaining });
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
