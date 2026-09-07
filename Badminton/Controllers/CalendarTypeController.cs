using Contract.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.CalendarType;

namespace BadmintonBE.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CalendarTypeController : ControllerBase
    {
        private readonly ICalendarTypeService _calendarTypeService;

        public CalendarTypeController(
            ICalendarTypeService calendarTypeService)
        {
            _calendarTypeService = calendarTypeService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _calendarTypeService.GetAllAsync();

            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _calendarTypeService.GetByIdAsync(id);

            if (result == null)
            {
                return NotFound(new
                {
                    message = "Calendar type not found."
                });
            }

            return Ok(result);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(
            [FromBody] CalendarTypeRequest request)
        {
            var result = await _calendarTypeService.CreateAsync(request);

            return Ok(result);
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] CalendarTypeRequest request)
        {
            var result = await _calendarTypeService.UpdateAsync(id, request);

            if (result == null)
            {
                return NotFound(new
                {
                    message = "Calendar type not found."
                });
            }

            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _calendarTypeService.DeleteAsync(id);

            if (!result)
            {
                return NotFound(new
                {
                    message = "Calendar type not found."
                });
            }

            return Ok(new
            {
                message = "Calendar type deleted successfully."
            });
        }
    }
}