using Contract.Services.Booking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.Booking;
using System.Security.Claims;

namespace BadmintonBE.API.Controllers
{
    /// <summary>
    /// BOOKING endpoints — the heart of the app. Creating a booking runs the
    /// full polymorphic pricing + slot-conflict flow; confirming payment hands
    /// off to the payment strategy.
    /// </summary>
    [ApiController]
    [Route("api/bookings")]
    [Authorize]
    public class BookingController : BaseApiController
    {
        private readonly IBookingService _svc;

        public BookingController(IBookingService svc) => _svc = svc;

        private int CurrentUserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        /// <summary>Create a booking for the authenticated user.</summary>
        [HttpPost]
        public async Task<IActionResult> Create(CreateBookingRequest request) =>
            Resp(await _svc.CreateAsync(request, CurrentUserId, CurrentUserName),
                "Booking created.");

        /// <summary>Cancel one of the user's bookings.</summary>
        [HttpPost("{id:int}/cancel")]
        public async Task<IActionResult> Cancel(int id)
        {
            await _svc.CancelAsync(id, CurrentUserId, CurrentUserName);
            return RespMsg("Booking cancelled.");
        }

        /// <summary>All bookings for the authenticated user.</summary>
        [HttpGet("mine")]
        public async Task<IActionResult> Mine() => Resp(await _svc.GetByUserAsync(CurrentUserId));

        /// <summary>Single booking by id (owner or admin).</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var isAdmin = User.IsInRole("Admin") || User.IsInRole("Staff");
            var view = await _svc.GetByIdAsync(id, isAdmin ? null : CurrentUserId);
            return Resp(view);
        }

        /// <summary>
        /// Which courts are free on a given date (public, for the booking UI).
        /// </summary>
        [HttpGet("free/{date}")]
        [AllowAnonymous]
        public async Task<IActionResult> FreeCourts([FromRoute] DateOnly date) =>
            Resp(await _svc.GetFreeCourtsAsync(date));
    }
}
