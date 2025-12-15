using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Rent.Models;
using Rent.DTO; // LoginUserDTO
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Rent.Data; // DataContext

namespace Rent.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;
        private readonly DataContext _db;

        public AuthController(SignInManager<User> signInManager, UserManager<User> userManager, DataContext db)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _db = db;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginUserDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // authenticate
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return Unauthorized(new { Message = "Invalid email or password" });

            // Use non-null username fallback to email to avoid nullable warnings
            var userNameForSignIn = user.UserName ?? user.Email ?? string.Empty;
            var result = await _signInManager.PasswordSignInAsync(userNameForSignIn, dto.Password, isPersistent: false, lockoutOnFailure: false);
            if (!result.Succeeded)
                return Unauthorized(new { Message = "Invalid email or password" });

            return Ok(new { Message = "Login successful" });
        }

        [HttpGet("check")]
        public IActionResult Check()
        {
            var isAuth = User?.Identity?.IsAuthenticated ?? false;
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            var cookies = Request.Cookies?.Keys.ToArray() ?? new string[0];
            var claims = isAuth ? User.Claims.Select(c => new { c.Type, c.Value }).ToArray() : new object[0];

            return Ok(new
            {
                IsAuthenticated = isAuth,
                AuthenticationType = User?.Identity?.AuthenticationType,
                UserName = User?.Identity?.Name,
                AuthHeader = authHeader,
                Cookies = cookies,
                Claims = claims
            });
        }

        [Authorize]
        [HttpGet("protected")]
        public IActionResult Protected()
        {
            return Ok(new { Message = "Access granted", User = User?.Identity?.Name });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok(new { Message = "Logout successful" });
        }

        // get current user
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            // fill names
            string firstName = user.First_name;
            string lastName = user.Last_name;
            try
            {
                if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
                {
                    var worker = await _db.Workers.FirstOrDefaultAsync(w => w.Email.ToLower() == (user.Email ?? string.Empty).ToLower());
                    if (worker != null)
                    {
                        if (string.IsNullOrWhiteSpace(firstName)) firstName = worker.First_name;
                        if (string.IsNullOrWhiteSpace(lastName)) lastName = worker.Last_name;
                    }
                }
            }
            catch { }

            return Ok(new
            {
                user.Id,
                user.UserName,
                user.Email,
                user.PhoneNumber,
                First_name = firstName,
                Last_name = lastName,
                user.Login,
                Roles = roles
            });
        }
    }
}