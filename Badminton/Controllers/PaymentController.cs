using Contract.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BadmintonBE.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost("{bookingId:int}")]
        [Authorize(Roles = "Customer,Admin")]
        public async Task<IActionResult> Create(
            int bookingId,
            [FromQuery] string paymentMethod = "Mock")
        {
            var userId = GetUserId();
            var role = User.FindFirstValue(ClaimTypes.Role);

            try
            {
                var result = await _paymentService.CreateAsync(
                    bookingId,
                    userId,
                    role!,
                    paymentMethod);

                if (result == null)
                    return NotFound(new
                    {
                        message = "Booking not found."
                    });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }

        [HttpGet("booking/{bookingId:int}")]
        public async Task<IActionResult> GetByBookingId(int bookingId)
        {
            var userId = GetUserId();
            var role = User.FindFirstValue(ClaimTypes.Role);

            var result = await _paymentService.GetByBookingIdAsync(
                bookingId,
                userId,
                role!);

            if (result == null)
                return NotFound(new
                {
                    message = "Payment not found."
                });

            return Ok(result);
        }

        [HttpPut("{bookingId:int}/paid")]
        [Authorize(Roles = "Customer,Admin")]
        public async Task<IActionResult> MarkAsPaid(
            int bookingId,
            [FromQuery] string transactionCode)
        {
            if (string.IsNullOrWhiteSpace(transactionCode))
            {
                return BadRequest(new
                {
                    message = "Transaction code is required."
                });
            }

            var userId = GetUserId();
            var role = User.FindFirstValue(ClaimTypes.Role);

            var result = await _paymentService.MarkAsPaidAsync(
                bookingId,
                userId,
                role!,
                transactionCode);

            if (!result)
            {
                return BadRequest(new
                {
                    message = "Payment cannot be completed."
                });
            }

            return Ok(new
            {
                message = "Payment completed successfully."
            });
        }

        private int GetUserId()
        {
            return int.Parse(
                User.FindFirstValue("UserId")!);
        }
    }
}