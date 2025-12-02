using Microsoft.AspNetCore.Mvc;
using Rent.Interfaces;

namespace Rent.Controllers
{
 [ApiController]
 [Route("api/[controller]")]
 public class RentalInfoController : ControllerBase
 {
 private readonly IRentalInfoRepository _rentalInfoRepository;

 public RentalInfoController(IRentalInfoRepository rentalInfoRepository)
 {
 _rentalInfoRepository = rentalInfoRepository;
 }

 [HttpGet]
 public IActionResult Get()
 {
 var infos = _rentalInfoRepository.GetRentalInfos();
 return Ok(infos);
 }
 }
}
