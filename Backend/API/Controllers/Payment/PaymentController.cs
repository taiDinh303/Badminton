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

        [HttpGet("reconciliation")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Reconciliation([FromQuery] int? branchId = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var result = await _svc.GetReconciliationAsync(branchId, new Contract.Repositories.UnitOfWork.Page(Math.Clamp(page, 1, 1000000), Math.Clamp(pageSize, 1, 100)));
            return Resp(new { items = result.Items, totalCount = result.TotalCount });
        }

        [HttpPost("init")]
        [Authorize]
        public async Task<IActionResult> Init(InitPaymentRequest request) =>
            Resp(await _svc.InitAsync(request, CurrentUserId), "Khởi tạo thanh toán thành công.");

        [HttpGet("callback/vnpay")]
        [AllowAnonymous]
        public async Task<IActionResult> VnPayCallback()
        {
            var result = await _svc.HandleWebhookAsync("VnPay", new(Request.Query["vnp_TxnRef"].ToString(), "", Request.QueryString.Value));
            return Ok(new Dictionary<string, string> { ["RspCode"] = result.Accepted ? "00" : "97", ["Message"] = result.Accepted ? "Confirm Success" : "Rejected" });
        }

        [HttpPost("callback/momo")]
        [AllowAnonymous]
        public async Task<IActionResult> MoMoCallback([FromBody] System.Text.Json.JsonElement body)
        {
            if (body.ValueKind != System.Text.Json.JsonValueKind.Object || !body.TryGetProperty("orderId", out var id)
                || id.ValueKind != System.Text.Json.JsonValueKind.String) return BadRequest();
            var result = await _svc.HandleWebhookAsync("MoMo", new(id.GetString()!, "", body.GetRawText()));
            return result.Accepted ? NoContent() : BadRequest();
        }

        [HttpPost("callback/zalopay")]
        [AllowAnonymous]
        public async Task<IActionResult> ZaloPayCallback([FromBody] System.Text.Json.JsonElement body)
        {
            try
            {
                if (body.ValueKind != System.Text.Json.JsonValueKind.Object || !body.TryGetProperty("data", out var data)
                    || data.ValueKind != System.Text.Json.JsonValueKind.String) return Ok(new { return_code = 2, return_message = "Rejected" });
                using var decoded = System.Text.Json.JsonDocument.Parse(data.GetString()!);
                if (decoded.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object
                    || !decoded.RootElement.TryGetProperty("app_trans_id", out var id) || id.ValueKind != System.Text.Json.JsonValueKind.String)
                    return Ok(new { return_code = 2, return_message = "Rejected" });
                var result = await _svc.HandleWebhookAsync("Zalopay", new(id.GetString()!, "", body.GetRawText()));
                return Ok(new { return_code = result.Accepted ? 1 : 2, return_message = result.Accepted ? "Success" : "Rejected" });
            }
            catch (System.Text.Json.JsonException) { return Ok(new { return_code = 2, return_message = "Rejected" }); }
        }

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

        [HttpPost("reconciliation/{paymentId:int}/refund")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> ReconcileRefund(int paymentId) =>
            Resp(await _svc.ReconcileRefundAsync(paymentId, CurrentUserId), "Đã xác nhận kết quả hoàn tiền với provider.");

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
