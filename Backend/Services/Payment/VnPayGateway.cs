using System.Security.Cryptography;
using System.Text;
using Badminton.Core.Domain;
using Badminton.Core.Enums;
using Badminton.Core.Store;

namespace Services
{
    /// <summary>Minimal VNPAY transport so tests can stub it out.</summary>
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

        public async Task<VnPayCreateResult> CreatePayUrlAsync(
            IReadOnlyDictionary<string, string> fields, CancellationToken ct)
        {
            var query = VnPaySigner.BuildQuery(_s.SecretKey, fields);
            var url = _s.Endpoint + "/CreatePay?";
            var resp = await _http.GetAsync(url + query, ct);
            if (!resp.IsSuccessStatusCode)
                return new VnPayCreateResult(false, null, null,
                    $"VnPay HTTP {(int)resp.StatusCode}.");
            var html = await resp.Content.ReadAsStringAsync(ct);
            var loc = resp.Headers.Location?.ToString();
            var redirect = !string.IsNullOrWhiteSpace(loc) ? loc : url + query;
            var payId = fields.TryGetValue("vnp_TxCode", out var tx) ? tx : null;
            _ = html; // body intentionally unused — VNPAY create-pay-url is a
                      // browser-redirect endpoint, not a JSON API.
            return new VnPayCreateResult(true, redirect, payId, null);
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

            var txCode = "BADMINTON" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var fields = new Dictionary<string, string>
            {
                ["vnp_Version"] = _s.Version,
                ["vnp_Command"] = "pay",
                ["vnp_TmnCode"] = _s.MerchantCode,
                ["vnp_Referer"] = string.Empty,
                ["vnp_TxnRef"] = txCode,
                ["vnp_Amount"] = ((long)(amount * 100)).ToString(), // VND dong → "0" decimal
                ["vnp_CurrType"] = _s.CurrType,
                ["vnp_BankCode"] = _s.BankCode,
                ["vnp_Locale"] = _s.Locale,
                ["vnp_CreateTime"] = timestamp,
                ["vnp_OrderInfo"] = "Badminton Booking " + bookingId,
                ["vnp_OrderType"] = "Booking",
                ["vnp_ReturnUrl"] = returnUrl,
                ["vnp_IpnUrl"] = _s.CallbackUrl ?? string.Empty
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
            var q = Microsoft.AspNetCore.WebUtilities
                .QueryHelpers.ParseQuery(raw.Body)
                .ToDictionary(kv => kv.Key, kv => kv.Value.ToString(),
                    System.StringComparer.OrdinalIgnoreCase);

            if (q.Count == 0)
                return Task.FromResult(new WebhookOutcome(false, false, raw.GatewayRef,
                    "webhook VnPay body rong (khong co tham so)."));

            if (!q.TryGetValue("vnp_ResponseCode", out var respCode))
                return Task.FromResult(new WebhookOutcome(false, false, raw.GatewayRef,
                    "webhook VnPay thieu vnp_ResponseCode."));

            if (!q.TryGetValue("vnp_SecureHash", out var expectedHash) ||
                string.IsNullOrWhiteSpace(expectedHash))
                return Task.FromResult(new WebhookOutcome(false, false, raw.GatewayRef,
                    "webhook VnPay thieu vnp_SecureHash."));

            if (!VnPaySigner.Verify(_s.SecretKey, expectedHash, q))
                return Task.FromResult(new WebhookOutcome(false, false, raw.GatewayRef,
                    "chu ky vnp_SecureHash cua VnPay thieu hoac sai."));

            var success = respCode is "00" or "02";
            return Task.FromResult(new WebhookOutcome(
                Verified: true, Success: success, raw.GatewayRef, null));
        }

        public Task<RefundResult> RefundAsync(string bookingId, decimal amount,
            string reason, CancellationToken ct = default) =>
            Task.FromResult(new RefundResult(true, amount, null));
    }
}
