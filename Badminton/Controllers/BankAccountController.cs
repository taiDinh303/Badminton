using Contract.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ModelViews.BankAccount;

namespace BadmintonBE.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BankAccountController : ControllerBase
    {
        private readonly IBankAccountService _bankAccountService;

        public BankAccountController(
            IBankAccountService bankAccountService)
        {
            _bankAccountService = bankAccountService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var userId = GetUserId();

            var result = await _bankAccountService.GetByUserIdAsync(userId);

            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var userId = GetUserId();

            var result = await _bankAccountService.GetByIdAsync(
                id,
                userId);

            if (result == null)
            {
                return NotFound(new
                {
                    message = "Bank account not found."
                });
            }

            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateBankAccountRequest request)
        {
            var userId = GetUserId();

            var result = await _bankAccountService.CreateAsync(
                request,
                userId);

            return Ok(result);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] UpdateBankAccountRequest request)
        {
            var userId = GetUserId();

            var result = await _bankAccountService.UpdateAsync(
                id,
                request,
                userId);

            if (result == null)
            {
                return NotFound(new
                {
                    message = "Bank account not found."
                });
            }

            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetUserId();

            var result = await _bankAccountService.DeleteAsync(
                id,
                userId);

            if (!result)
            {
                return NotFound(new
                {
                    message = "Bank account not found."
                });
            }

            return Ok(new
            {
                message = "Bank account deleted successfully."
            });
        }

        private int GetUserId()
        {
            return int.Parse(
                User.FindFirstValue("UserId")!);
        }
    }
}