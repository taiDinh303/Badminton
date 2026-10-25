using Contract.Repositories.UnitOfWork;
using Contract.Services.Booking;
using Badminton.Core.Base;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.Booking;

namespace Badminton.API.Controllers
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

        [HttpPost("quote")]
        public async Task<IActionResult> Quote([FromBody] CreateBookingRequest request) =>
            Resp(await _svc.QuoteAsync(request, CurrentUserId));

        [HttpGet("mine")]
        public async Task<IActionResult> Mine(
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var (items, total) = await _svc.GetByUserPagedAsync(
                CurrentUserId, status, new Page(page, pageSize));
            var paged = new BasePaginatedList<BookingView>(items, total, page, pageSize);
            return Resp(paged);
        }

        [HttpGet("all")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> All(
            [FromQuery] string? status,
            [FromQuery] int? branchId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100,
            [FromQuery] string? search = null, [FromQuery] DateOnly? date = null, [FromQuery] bool needsAttendance = false)
        {
            var (items, total) = await _svc.GetAllPagedAsync(
                status, branchId, new Page(page, pageSize), search, date, needsAttendance);
            var paged = new BasePaginatedList<BookingView>(items, total, page, pageSize);
            return Resp(paged);
        }

        [HttpPost("{id:int}/cancel")]
        public async Task<IActionResult> Cancel(int id)
        {
            var view = await _svc.CancelAsync(id, CurrentUserId);
            return Resp(view, "Hủy đặt chỗ thành công.");
        }

        // ── Lifecycle: Confirmed → Completed / NoShow (Q1=B, Q2=A) ───────

        [HttpPost("{id:int}/complete")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Complete(int id, [FromQuery] string? note = null)
        {
            var view = await _svc.MarkCompletedAsync(id, CurrentUserId, note);
            return Resp(view, "Đã đánh dấu hoàn thành.");
        }

        [HttpPost("{id:int}/no-show")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> NoShow(int id, [FromQuery] string? note = null)
        {
            var view = await _svc.MarkNoShowAsync(id, CurrentUserId, note);
            return Resp(view, "Đã gắn cờ no-show.");
        }

        [HttpGet("availability/{branchId:int}/{date}")]
        [AllowAnonymous]
        public async Task<IActionResult> Availability(int branchId, [FromRoute] string date) =>
            Resp(await _svc.GetAvailabilityAsync(branchId,
                DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture)));
    }
}
