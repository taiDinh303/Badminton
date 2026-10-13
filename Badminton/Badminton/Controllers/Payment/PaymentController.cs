using Contract.Services.Payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.Payment;

namespace BadmintonBE.API.Controllers
{
    [ApiController]
    [Route("api/v1/payments")]
    public class PaymentController : BaseApiController
    {
        private readonly IPaymentService _svc;
        public PaymentController(IPaymentService svc) => _svc = svc;

        [HttpPost("init")]
        [Authorize]
        public async Task<IActionResult> Init(InitPaymentRequest request) =>
            Resp(await _svc.InitAsync(request, CurrentUserId), "Khởi tạo thanh toán thành công.");

        [HttpPost("webhook/{provider}")]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook(string provider, PaymentWebhookRequest raw)
        {
            await _svc.HandleWebhookAsync(provider, raw);
            return RespMsg("Webhook đã xử lý.");
        }

        [HttpPost("{bookingId:int}/on-site")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> ConfirmOnSite(int bookingId,
            [FromQuery] string method = "cash", [FromQuery] string? note = null)
            => Resp(await _svc.ConfirmOnSiteAsync(bookingId, method, CurrentUserId, note),
                "Xác nhận thanh toán tại quầy thành công.");

        [HttpPost("{bookingId:int}/refund")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Refund(int bookingId, [FromQuery] string? reason = null)
            => Resp(await _svc.RefundAsync(bookingId, CurrentUserId, reason),
                "Hoàn tiền thành công (record mới).");

        [HttpGet("booking/{bookingId:int}")]
        [Authorize]
        public async Task<IActionResult> ForBooking(int bookingId) =>
            Resp(await _svc.GetByBookingAsync(bookingId));
    }
}
