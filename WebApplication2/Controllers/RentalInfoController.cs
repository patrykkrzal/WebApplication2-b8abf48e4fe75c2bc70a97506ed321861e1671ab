using Microsoft.AspNetCore.Mvc;
using Rent.Data;
using Rent.Models;
using System.Linq;

namespace Rent.Controllers
{
 [ApiController]
 [Route("api/[controller]")]
 public class RentalInfoController : ControllerBase
 {
 private readonly DataContext _dbContext;

 public RentalInfoController(DataContext dbContext)
 {
 _dbContext = dbContext;
 }

 [HttpGet]
 public IActionResult Get()
 {
 var infos = _dbContext.RentalInfo.ToList();
 return Ok(infos);
 }
 }
}
