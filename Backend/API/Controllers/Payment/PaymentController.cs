using Contract.Services.Payment;
using Badminton.Core.Base;
using Badminton.Core.Store;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.Payment;

namespace Badminton.API.Controllers
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

        /// <summary>
        /// Gateway webhook callback. Returns 2xx so the provider stops retrying
        /// when the payload was accepted or was an idempotent duplicate;
        /// returns 4xx when the payload was rejected (bad signature / unknown
        /// ref) so the provider does NOT keep hammering a permanently-broken
        /// call.
        /// </summary>
        [HttpPost("webhook/{provider}")]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook(string provider, [FromBody] PaymentWebhookRequest raw)
        {
            var result = await _svc.HandleWebhookAsync(provider, raw);
            if (result.Accepted)
                return Ok(new BaseResponse<string>(StatusCodeHelper.OK,
                    ResponseCodeConstants.SUCCESS,
                    result.Reason ?? "ok"));
            return BadRequest(new BaseResponse<string>(StatusCodeHelper.BadRequest,
                ResponseCodeConstants.BAD_REQUEST,
                result.Reason ?? "webhook rejected"));
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
        public async Task<IActionResult> ForBooking(int bookingId)
            => Resp(await _svc.GetByBookingAsync(bookingId, CurrentUserId));
    }
}
