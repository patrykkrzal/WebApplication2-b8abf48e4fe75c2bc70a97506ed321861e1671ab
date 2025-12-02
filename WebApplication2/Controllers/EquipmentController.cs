using Microsoft.AspNetCore.Mvc;
using Rent.Data;
using Rent.DTO;
using Rent.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore; // for Database facade
using Microsoft.AspNetCore.Authorization;
using Rent.Enums;

namespace Rent.Controllers
{
    [ApiController]
    [Route("api/equipment")]
    public class EquipmentController : ControllerBase
    {
        private readonly DataContext dbContext;

        public EquipmentController(DataContext dbContext)
        {
            this.dbContext = dbContext;
        }

        [HttpGet]
        public IActionResult GetAllEquipment()
        {
            var allEquipment = dbContext.Equipment.ToList();
            return Ok(allEquipment);
        }

        [HttpGet("{id:int}")]
        public IActionResult GetById(int id)
        {
            var eq = dbContext.Equipment.Find(id);
            return eq is null ? NotFound() : Ok(eq);
        }

        private decimal ResolvePrice(EquipmentType type, Size size)
        {
            // Stałe ceny według typu + ewentualny rozmiar
            return type switch
            {
                EquipmentType.Skis => size switch
                {
                    Size.Small =>120m,
                    Size.Medium =>130m,
                    Size.Large =>140m,
                    _ =>130m
                },
                EquipmentType.Helmet =>35m,
                EquipmentType.Gloves =>15m,
                EquipmentType.Poles =>22m,
                EquipmentType.Snowboard =>160m,
                EquipmentType.Goggles =>55m,
                _ =>0m
            };
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
                    UnitPrice = g.First().Price
                })
                .ToList();
            return Ok(grouped);
        }

        [Authorize(Roles = "Admin,Worker")]
        [HttpPost("add")] // unique route to avoid Swagger conflicts
        public IActionResult AddEquipment([FromBody] CreateEquipmentDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var price = ResolvePrice(dto.Type, dto.Size);

            // use stored procedure spAddEquipment
            dbContext.Database.ExecuteSqlRaw(
                "EXEC dbo.spAddEquipment @p0, @p1, @p2",
                (int)dto.Type,
                (int)dto.Size,
                price
            );

            // Return refreshed list or simple acknowledgment
            return Ok(new { Message = "Equipment added", dto.Type, dto.Size, Price = price });
        }

        // DELETE one available (not reserved, in warehouse) item for given type+size
        [Authorize(Roles = "Admin,Worker")]
        [HttpDelete("delete")]
        public IActionResult DeleteOne([FromBody] CreateEquipmentDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var entity = dbContext.Equipment
                .Where(e => e.Type == dto.Type && e.Size == dto.Size && e.Is_In_Werehouse && !e.Is_Reserved)
                .FirstOrDefault();
            if (entity == null)
            {
                return NotFound(new { Message = "Equipment not found or unavailable", dto.Type, dto.Size });
            }
            dbContext.Equipment.Remove(entity);
            dbContext.SaveChanges();
            var remaining = dbContext.Equipment.Count(e => e.Type == dto.Type && e.Size == dto.Size && e.Is_In_Werehouse && !e.Is_Reserved);
            return Ok(new { Message = "Deleted one equipment item", dto.Type, dto.Size, Remaining = remaining });
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
