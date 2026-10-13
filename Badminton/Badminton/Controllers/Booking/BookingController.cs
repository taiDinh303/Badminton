using Contract.Services.Booking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.Booking;

namespace BadmintonBE.API.Controllers
{
    [ApiController]
    [Route("api/v1/bookings")]
    [Authorize]
    public class BookingController : BaseApiController
    {
        private readonly IBookingService _svc;
        public BookingController(IBookingService svc) => _svc = svc;

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateBookingRequest request) =>
            Resp(await _svc.CreateAsync(request, CurrentUserId), "Đặt chỗ thành công (đang chờ thanh toán).");

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id) =>
            Resp(await _svc.GetByIdAsync(id, CurrentUserId));

        [HttpGet("mine")]
        public async Task<IActionResult> Mine([FromQuery] string? status) =>
            Resp(await _svc.GetByUserAsync(CurrentUserId, status));

        [HttpPost("{id:int}/cancel")]
        public async Task<IActionResult> Cancel(int id)
        {
            var view = await _svc.CancelAsync(id, CurrentUserId);
            return Resp(view, "Hủy đặt chỗ thành công.");
        }

        [HttpGet("availability/{branchId:int}/{date}")]
        [AllowAnonymous]
        public async Task<IActionResult> Availability(int branchId, [FromRoute] string date) =>
            Resp(await _svc.GetAvailabilityAsync(branchId,
                DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture)));
    }
}
