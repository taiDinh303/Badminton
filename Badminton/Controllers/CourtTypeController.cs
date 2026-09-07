using Contract.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.CourtType;

namespace BadmintonBE.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CourtTypeController : ControllerBase
    {
        private readonly ICourtTypeService _courtTypeService;

        public CourtTypeController(ICourtTypeService courtTypeService)
        {
            _courtTypeService = courtTypeService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _courtTypeService.GetAllAsync();
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _courtTypeService.GetByIdAsync(id);

            if (result == null)
            {
                return NotFound(new
                {
                    message = "Court type not found."
                });
            }

            return Ok(result);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(
            [FromBody] CourtTypeRequest request)
        {
            var result = await _courtTypeService.CreateAsync(request);

            return Ok(result);
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] CourtTypeRequest request)
        {
            var result = await _courtTypeService.UpdateAsync(id, request);

            if (result == null)
            {
                return NotFound(new
                {
                    message = "Court type not found."
                });
            }

            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _courtTypeService.DeleteAsync(id);

            if (!result)
            {
                return NotFound(new
                {
                    message = "Court type not found."
                });
            }

            return Ok(new
            {
                message = "Court type deleted successfully."
            });
        }
    }
}