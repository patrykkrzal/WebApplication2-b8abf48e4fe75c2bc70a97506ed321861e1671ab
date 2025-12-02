using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Rent.Models;
using Rent.DTO;
using System.Linq;
using System.Threading.Tasks;

namespace Rent.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RegisterController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public RegisterController(UserManager<User> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpPost]
        public async Task<IActionResult> Register([FromBody] RegisterUserDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = new User
            {
                UserName = dto.UserName,
                Email = dto.Email,
                PhoneNumber = dto.ContactNumber,
                First_name = dto.FirstName,
                Last_name = dto.LastName,
                Login = dto.UserName
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { Errors = errors });
            }

            const string roleName = "User";
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                var roleCreate = await _roleManager.CreateAsync(new IdentityRole(roleName));
                if (!roleCreate.Succeeded)
                {
                    var errors = roleCreate.Errors.Select(e => e.Description);
                    return BadRequest(new { Errors = errors, Message = "Nie udało się utworzyć roli 'User'." });
                }
            }

            var addToRole = await _userManager.AddToRoleAsync(user, roleName);
            if (!addToRole.Succeeded)
            {
                var errors = addToRole.Errors.Select(e => e.Description);
                return BadRequest(new { Errors = errors, Message = "Nie udało się przypisać roli 'User'." });
            }

            return Ok(new
            {
                Message = "User registered successfully",
                UserId = user.Id,
                Roles = await _userManager.GetRolesAsync(user)
            });
        }
    }
}
