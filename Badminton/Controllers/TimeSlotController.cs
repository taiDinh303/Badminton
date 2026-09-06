using Contract.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BadmintonBE.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TimeSlotController : ControllerBase
    {
        private readonly ITimeSlotService _timeSlotService;

        public TimeSlotController(ITimeSlotService timeSlotService)
        {
            _timeSlotService = timeSlotService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _timeSlotService.GetAllAsync();

            return Ok(result);
        }

        [HttpGet("{timeSlotId:int}")]
        public async Task<IActionResult> GetById(int timeSlotId)
        {
            var result = await _timeSlotService
                .GetByIdAsync(timeSlotId);

            if (result == null)
            {
                return NotFound(new
                {
                    message = "Time slot not found."
                });
            }

            return Ok(result);
        }
    }
}