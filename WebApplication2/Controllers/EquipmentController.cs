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
            // Return equipment rows but expose UnitPrice resolved from priceResolver
            var allEquipment = dbContext.Equipment
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
            var grouped = dbContext.Equipment
                .Where(e => e.Is_In_Werehouse && !e.Is_Reserved)
                .GroupBy(e => new { e.Type, e.Size })
                .Select(g => new
                {
                    Type = g.Key.Type.ToString(),
                    Size = g.Key.Size.ToString(),
                    Count = g.Count(),
                    UnitPrice = priceResolver.ResolvePrice(g.Key.Type, g.Key.Size)
                })
                .ToList();
            return Ok(grouped);
        }

        [Authorize(Roles = "Admin,Worker")]
        [HttpPost("add")] // unique route to avoid Swagger conflicts
        public IActionResult AddEquipment([FromBody] CreateEquipmentDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var price = dto.Price ?? priceResolver.ResolvePrice(dto.Type, dto.Size);
            var qty = dto.Quantity ??1;

            // execute stored proc qty times (spAddEquipment handles single insert)
            for (int i =0; i < qty; i++)
            {
                dbContext.Database.ExecuteSqlRaw(
                    "EXEC dbo.spAddEquipment @p0, @p1, @p2",
                    (int)dto.Type,
                    (int)dto.Size,
                    price
                );
            }

            return Ok(new { Message = "Equipment added", dto.Type, dto.Size, Price = price, Quantity = qty });
        }

        // DELETE one available (not reserved, in warehouse) item for given type+size
        [Authorize(Roles = "Admin,Worker")]
        [HttpDelete("delete")]
        public IActionResult DeleteOne([FromBody] CreateEquipmentDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var qty = dto.Quantity ??1;
            int deleted =0;
            for (int i =0; i < qty; i++)
            {
                var entity = dbContext.Equipment
                    .Where(e => e.Type == dto.Type && e.Size == dto.Size && e.Is_In_Werehouse && !e.Is_Reserved)
                    .FirstOrDefault();
                if (entity == null) break;
                dbContext.Equipment.Remove(entity);
                deleted++;
            }
            dbContext.SaveChanges();
            var remaining = dbContext.Equipment.Count(e => e.Type == dto.Type && e.Size == dto.Size && e.Is_In_Werehouse && !e.Is_Reserved);
            return Ok(new { Message = "Deleted equipment items", dto.Type, dto.Size, Deleted = deleted, Remaining = remaining });
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
