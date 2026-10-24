using System.Security.Cryptography;
using System.Text;
using Badminton.Core.Domain;
using Badminton.Core.Enums;
using Badminton.Core.Store;

namespace Services
{
    public interface IVnPayClient
    {
        Task<VnPayCreateResult> CreatePayUrlAsync(
            IReadOnlyDictionary<string, string> fields, CancellationToken ct);
    }

    public sealed record VnPayCreateResult(bool Ok, string? RedirectUrl,
        string? GatewayRef, string? Error);

    public sealed class HttpVnPayClient : IVnPayClient
    {
        private readonly HttpClient _http;
        private readonly VnPaySettings _s;

        public HttpVnPayClient(HttpClient http, VnPaySettings settings)
        {
            _http = http; _s = settings;
        }

        public Task<VnPayCreateResult> CreatePayUrlAsync(
            IReadOnlyDictionary<string, string> fields, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            // VNPAY checkout is a signed browser redirect; the backend must not fetch it.
            var query = VnPaySigner.BuildQuery(_s.SecretKey, fields);
            return Task.FromResult(new VnPayCreateResult(true,
                _s.Endpoint + "?" + query, fields["vnp_TxnRef"], null));
        }

    }

    public sealed class VnPayGateway : IPaymentGateway
    {
        private readonly VnPaySettings _s;
        private readonly IVnPayClient _client;

        public VnPayGateway(VnPaySettings settings, IVnPayClient client)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public PaymentMethod Method { get; } = PaymentMethod.VnPay;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_s.MerchantCode) &&
            !string.IsNullOrWhiteSpace(_s.SecretKey);

        public async Task<PaymentInitResult> InitAsync(string bookingId, decimal amount,
            string returnUrl, CancellationToken ct = default)
        {
            if (!IsConfigured)
                return PaymentInitResult.Fail(
                    "VnPay chua duoc cau hinh (thieu MerchantCode/SecretKey), khong the thanh toan.");

            if (amount <= 0 || amount != decimal.Truncate(amount) || amount > int.MaxValue)
                return PaymentInitResult.Fail("Số tiền VNPAY không hợp lệ.");
            var txCode = bookingId;
            var timestamp = Badminton.Core.Utils.HanoiTime.HanoiNow().ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
            var fields = new Dictionary<string, string>
            {
                ["vnp_Version"] = _s.Version,
                ["vnp_Command"] = "pay",
                ["vnp_TmnCode"] = _s.MerchantCode,
                ["vnp_IpAddr"] = "127.0.0.1",
                ["vnp_TxnRef"] = txCode,
                ["vnp_Amount"] = checked((long)(amount * 100)).ToString(System.Globalization.CultureInfo.InvariantCulture), // VND dong → "0" decimal
                ["vnp_CurrCode"] = _s.CurrType,
                ["vnp_BankCode"] = _s.BankCode,
                ["vnp_Locale"] = _s.Locale,
                ["vnp_CreateDate"] = timestamp,
                ["vnp_OrderInfo"] = "Badminton Booking " + bookingId,
                ["vnp_OrderType"] = "other",
                ["vnp_ReturnUrl"] = returnUrl
            };

            var init = await _client.CreatePayUrlAsync(fields, ct);
            if (!init.Ok)
                return PaymentInitResult.Fail(init.Error ?? "VnPay tra loi loi.");
            if (string.IsNullOrWhiteSpace(init.RedirectUrl))
                return PaymentInitResult.Fail("VnPay khong tra ve URL chi thanh toan.");
            return PaymentInitResult.Success(init.GatewayRef ?? txCode, init.RedirectUrl);
        }

        public Task<WebhookOutcome> VerifyWebhookAsync(RawWebhook raw, CancellationToken ct = default)
        {
            if (!IsConfigured)
                return Task.FromResult(new WebhookOutcome(false, false, raw.GatewayRef,
                    "VnPay chua duoc cau hinh, khoi xu webhook."));

            if (string.IsNullOrWhiteSpace(raw.Body))
                return Task.FromResult(new WebhookOutcome(false, false, raw.GatewayRef,
                    "webhook VnPay thieu body, khong the xac minh."));

            // The VNPAY ipn payload arrives as a URL-encoded query string.
            var parsed = System.Web.HttpUtility.ParseQueryString(raw.Body.TrimStart('?'));
            var q = parsed.AllKeys.Where(key => key is not null)
                .ToDictionary(key => key!, key => parsed[key!] ?? "",
                    System.StringComparer.OrdinalIgnoreCase);

            if (parsed.AllKeys.Any(k => k is null || parsed.GetValues(k)?.Length != 1))
                return Task.FromResult(new WebhookOutcome(false, false, null, "Duplicate webhook fields."));
            if (!q.TryGetValue("vnp_SecureHash", out var hash) || !VnPaySigner.Verify(_s.SecretKey, hash, q))
                return Task.FromResult(new WebhookOutcome(false, false, null, "Chữ ký VNPAY không hợp lệ."));
            if (!q.TryGetValue("vnp_TxnRef", out var reference) || !string.Equals(reference, raw.GatewayRef, StringComparison.Ordinal)
                || !q.TryGetValue("vnp_TmnCode", out var merchant) || merchant != _s.MerchantCode
                || !q.TryGetValue("vnp_Amount", out var value) || !long.TryParse(value, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var cents) || cents < 0 || cents % 100 != 0
                || !q.TryGetValue("vnp_ResponseCode", out var response) || !q.TryGetValue("vnp_TransactionStatus", out var status)
                || (q.TryGetValue("vnp_CurrCode", out var currency) && currency != "VND"))
                return Task.FromResult(new WebhookOutcome(false, false, null, "Webhook VNPAY không khớp đơn/merchant/số tiền hoặc thiếu trạng thái."));
            return Task.FromResult(new WebhookOutcome(true, response == "00" && status == "00", reference, null, cents / 100m, "VND",
                q.GetValueOrDefault("vnp_TransactionNo")));
        }

        public Task<RefundResult> RefundAsync(string bookingId, decimal amount,
            string reason, CancellationToken ct = default) =>
            Task.FromResult(RefundResult.Fail("ONLINE_REFUND_UNAVAILABLE: Chưa có kết nối hoàn tiền online. Giao dịch chưa được hoàn; cần xử lý và đối soát với nhà cung cấp."));
    }
}
