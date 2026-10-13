using Contract.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ModelViews.Booking;

namespace BadmintonBE.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BookingController : ControllerBase
    {
        private readonly IBookingService _bookingService;

        public BookingController(IBookingService bookingService)
        {
            _bookingService = bookingService;
        }

        [HttpPost]
        [Authorize(Roles = "Customer,Admin")]
        public async Task<IActionResult> Create(CreateBookingRequest request)
        {
            var userId = GetUserId();

            try
            {
                var result = await _bookingService.CreateAsync(request, userId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }

        [HttpGet("{bookingId:int}")]
        public async Task<IActionResult> GetById(int bookingId)
        {
            var userId = GetUserId();
            var role = User.FindFirstValue(ClaimTypes.Role);

            var result = await _bookingService
                .GetByIdAsync(bookingId, userId, role!);

            if (result == null)
            {
                return NotFound(new
                {
                    message = "Booking not found."
                });
            }

            return Ok(result);
        }

        [HttpGet("user/{userId:int}")]
        public async Task<IActionResult> GetByUserId(int userId)
        {
            var currentUserId = GetUserId();

            var role = User.FindFirstValue(ClaimTypes.Role);

            if (role != "Admin" && role != "Staff" &&
                currentUserId != userId)
            {
                return Forbid();
            }

        [HttpGet("get-all")]
        //[Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll()
        {
            var result = await _bookingService.GetAllAsync();
            return Ok(new BaseResponse<List<BookingResponseModelView>>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: result));
        }

        [HttpPut("{bookingId:int}/confirm")]
        [Authorize(Roles = "Customer,Admin")]
        public async Task<IActionResult> Confirm(int bookingId)
        {
            var userId = GetUserId();
            var role = User.FindFirstValue(ClaimTypes.Role);

            var result = await _bookingService.ConfirmAsync(
                bookingId,
                userId,
                role!);

            if (!result)
                return BadRequest(new
                {
                    message = "Booking cannot be confirmed."
                });

            return Ok(new
            {
                message = "Booking confirmed successfully."
            });
        }

        [HttpPut("{bookingId:int}/cancel")]
        [Authorize(Roles = "Customer,Admin")]
        public async Task<IActionResult> Cancel(int bookingId)
        {
            var result = await _bookingService.GetByUserIdAsync(
                User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? string.Empty);
            return Ok(new BaseResponse<List<BookingResponseModelView>>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: result));
        }

        [HttpPut("{bookingId:int}/complete")]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> Complete(int bookingId)
        {
            var userId = GetUserId();
            var role = User.FindFirstValue(ClaimTypes.Role);

            var result = await _bookingService.CompleteAsync(
                bookingId,
                userId,
                role!);

            if (!result)
            {
                return BadRequest(new
                {
                    message = "Booking cannot be completed."
                });
            }

            return Ok(new
            {
                message = "Booking completed successfully."
            });
        }

        [HttpGet("my-bookings")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetMyBookings()
        {
            var userId = GetUserId();

            var result = await _bookingService
                .GetMyBookingsAsync(userId);

            return Ok(result);
        }

        [HttpDelete("soft-delete/{id}")]
        //[Authorize(Roles = "User,Admin")]
        public async Task<IActionResult> SoftDelete(Guid id)
        {
            await _bookingService.SoftDeleteAsync(id);
            return Ok(new BaseResponse<string>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: "Booking soft-deleted successfully!"));
        }

        [HttpDelete("delete/{id}")]
        //[Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _bookingService.DeleteAsync(id);
            return Ok(new BaseResponse<string>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: "Booking deleted permanently!"));
        }
    }
}