using Contract.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BadmintonBE.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CourtController : ControllerBase
    {
        private readonly ICourtService _courtService;

        public CourtController(ICourtService courtService)
        {
            _courtService = courtService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _courtService.GetAllAsync();
            return Ok(result);
        }

        [HttpGet("{courtId:int}")]
        public async Task<IActionResult> GetById(int courtId)
        {
            var result = await _courtService.GetByIdAsync(courtId);

            if (result == null)
                return NotFound(new { message = "Court not found." });

            return Ok(result);
        }
    }
}