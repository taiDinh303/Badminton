using Contract.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.Branch;

namespace BadmintonBE.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BranchController : ControllerBase
    {
        private readonly IBranchService _branchService;

        public BranchController(IBranchService branchService)
        {
            _branchService = branchService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _branchService.GetAllAsync();

            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _branchService.GetByIdAsync(id);

            if (result == null)
                return NotFound(new
                {
                    message = "Branch not found."
                });

            return Ok(result);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] BranchRequest request)
        {
            var result = await _branchService.CreateAsync(request);

            return Ok(result);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] BranchRequest request)
        {
            var result = await _branchService.UpdateAsync(id, request);

            if (result == null)
                return NotFound(new
                {
                    message = "Branch not found."
                });

            return Ok(result);
        }
        
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _branchService.DeleteAsync(id);

            if (!result)
                return NotFound(new
                {
                    message = "Branch not found."
                });

            return Ok(new
            {
                message = "Branch deleted successfully."
            });
        }
    }
}