using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rent.Data;
using Rent.DTO;
using Rent.Models;
using System.Linq;
using System.Threading.Tasks;

namespace Rent.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WorkersController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly DataContext _db;

        public WorkersController(UserManager<User> userManager, DataContext db)
        {
            _userManager = userManager;
            _db = db;
        }

        [HttpPost]
        public async Task<IActionResult> Register([FromBody] CreateWorkerDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = new User
            {
                UserName = dto.Email,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                First_name = dto.FirstName,
                Last_name = dto.LastName,
                Login = dto.Email,
            };

            var createUser = await _userManager.CreateAsync(user, dto.Password);
            if (!createUser.Succeeded)
                return BadRequest(new { Errors = createUser.Errors.Select(e => e.Description) });

            await _userManager.AddToRoleAsync(user, "Worker");

            // Resolve RentalInfoId automatically: if provided id doesn't exist (or is0),
            // fallback to the first existing RentalInfo id.
            int resolvedRentalInfoId = dto.RentalInfoId;
            bool exists = resolvedRentalInfoId > 0 && await _db.RentalInfo.AnyAsync(r => r.Id == resolvedRentalInfoId);
            if (!exists)
            {
                var first = await _db.RentalInfo.OrderBy(r => r.Id).Select(r => r.Id).FirstOrDefaultAsync();
                if (first == 0)
                {
                    return BadRequest(new { Message = "Brak dostępnego RentalInfo w bazie danych." });
                }
                resolvedRentalInfoId = first;
            }

            var worker = new Worker
            {
                First_name = dto.FirstName,
                Last_name = dto.LastName,
                Email = dto.Email,
                Phone_number = dto.PhoneNumber,
                Address = dto.Address,
                WorkStart = dto.WorkStart,
                WorkEnd = dto.WorkEnd,
                Working_Days = dto.Working_Days,
                Job_Title = dto.Job_Title,
                RentalInfoId = resolvedRentalInfoId
            };

            _db.Workers.Add(worker);
            await _db.SaveChangesAsync();

            return Ok(new { Message = "Worker created successfully", UserId = user.Id, WorkerId = worker.Id, RentalInfoId = resolvedRentalInfoId });
        }

        // DELETE worker & linked user by email
        [HttpDelete("{email}")]
        public async Task<IActionResult> DeleteByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return BadRequest("Email wymagany");
            var normalized = email.Trim().ToLower();
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalized);
            if (user == null) return NotFound(new { Message = "Nie znaleziono użytkownika." });
            // remove worker row if exists
            var worker = await _db.Workers.FirstOrDefaultAsync(w => w.Email != null && w.Email.ToLower() == normalized);
            if (worker != null) { _db.Workers.Remove(worker); }
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded) return StatusCode(500, new { Message = "Nie udało się usunąć użytkownika", Errors = result.Errors.Select(e => e.Description) });
            await _db.SaveChangesAsync();
            return Ok(new { Message = "Usunięto użytkownika/pracownika", Email = email });
        }
    }
}