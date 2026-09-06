using Contract.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BadmintonBE.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BookingDetailController : ControllerBase
    {
        private readonly IBookingDetailService _bookingDetailService;

        public BookingDetailController(
            IBookingDetailService bookingDetailService)
        {
            _bookingDetailService = bookingDetailService;
        }

        [HttpGet("{bookingDetailId:int}")]
        public async Task<IActionResult> GetById(int bookingDetailId)
        {
            var userId = GetUserId();

            var role = User.FindFirstValue(ClaimTypes.Role);

            var result = await _bookingDetailService
                .GetByIdAsync(bookingDetailId, userId, role!);

            if (result == null)
            {
                return NotFound(new
                {
                    message = "Booking detail not found."
                });
            }

            return Ok(result);
        }

        [HttpGet("booking/{bookingId:int}")]
        public async Task<IActionResult> GetByBookingId(int bookingId)
        {
            var userId = GetUserId();

            var role = User.FindFirstValue(ClaimTypes.Role);

            var result = await _bookingDetailService
                .GetByBookingIdAsync(bookingId, userId, role!);

            if (result == null)
            {
                return NotFound(new
                {
                    message = "Booking not found."
                });
            }

            return Ok(result);
        }

        [HttpPut("{bookingDetailId:int}/cancel")]
        [Authorize(Roles = "Customer,Admin")]
        public async Task<IActionResult> Cancel(int bookingDetailId)
        {
            var userId = GetUserId();

            var role = User.FindFirstValue(ClaimTypes.Role);

            var result = await _bookingDetailService
                .CancelAsync(bookingDetailId, userId, role!);

            if (!result)
            {
                return NotFound(new
                {
                    message = "Booking detail not found."
                });
            }

            return Ok(new
            {
                message = "Booking detail cancelled successfully."
            });
        }

        private int GetUserId()
        {
            return int.Parse(
                User.FindFirstValue("UserId")!);
        }
    }
}