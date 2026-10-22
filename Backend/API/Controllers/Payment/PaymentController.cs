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
        private readonly Badminton.Core.Domain.IMetricsReporter? _metrics;
        public PaymentController(IPaymentService svc,
            Badminton.Core.Domain.IMetricsReporter? metrics = null)
        {
            _svc = svc;
            _metrics = metrics;
        }

        [HttpGet("methods")]
        [AllowAnonymous]
        public IActionResult Methods([FromServices] IConfiguration configuration) => Resp(
            string.Equals(configuration["Payment:Provider"], "onsite", StringComparison.OrdinalIgnoreCase)
                ? new[] { "Cash", "QR" }
                : new[] { "Cash", "QR", "VnPay", "MoMo", "Zalopay" });

        [HttpPost("init")]
        [Authorize]
        public async Task<IActionResult> Init(InitPaymentRequest request) =>
            Resp(await _svc.InitAsync(request, CurrentUserId), "Khởi tạo thanh toán thành công.");

        [HttpPost("webhook/{provider}")]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook(string provider, [FromBody] PaymentWebhookRequest raw)
        {
            var result = await _svc.HandleWebhookAsync(provider, raw);
            try
            {
                var outcome = result.IdempotentDuplicate ? "duplicate"
                    : result.Accepted ? "applied" : "rejected";
                _metrics?.CountWebhook(provider, outcome);
            }
            catch { /* metrics must never block webhook ack */ }
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
