using Contract.Services.Payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.Payment;
using System.Security.Claims;

namespace BadmintonBE.API.Controllers
{
    /// <summary>
    /// PAYMENT endpoints. Confirming a payment is the POLYMORPHISM showcase:
    /// the same endpoint serves Cash / BankTransfer / QRCode because the
    /// service routes to the matching strategy via the factory.
    /// </summary>
    [ApiController]
    [Route("api/payments")]
    [Authorize]
    public class PaymentController : BaseApiController
    {
        private readonly IPaymentService _svc;

        public PaymentController(IPaymentService svc) => _svc = svc;

        private int CurrentUserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        /// <summary>Confirm (pay) a pending booking via the chosen method.</summary>
        [HttpPost("{bookingId:int}/confirm")]
        public async Task<IActionResult> Confirm(
            int bookingId, [FromBody] ConfirmPaymentRequest request)
        {
            request = request with { BookingId = bookingId };
            var view = await _svc.ConfirmPaymentAsync(request, CurrentUserId, CurrentUserName);
            return Resp(view, "Payment confirmed.");
        }

        /// <summary>Refund a paid booking (admin only).</summary>
        [HttpPost("{paymentId:int}/refund")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Refund(int paymentId)
        {
            await _svc.RefundAsync(paymentId, CurrentUserName);
            return RespMsg("Payment refunded.");
        }

        /// <summary>Get the payment record for a booking.</summary>
        [HttpGet("booking/{bookingId:int}")]
        public async Task<IActionResult> ForBooking(int bookingId) =>
            Resp(await _svc.GetByBookingAsync(bookingId));
    }
}
