using System.Security.Cryptography;
using System.Text;
using Badminton.Core.Domain;
using Badminton.Core.Enums;
using Badminton.Core.Store;

namespace Services
{
    public static class ZpSigner
    {
        public static string Hmac(string apiSecret, string message)
        {
            using var h = new HMACSHA512(Encoding.UTF8.GetBytes(apiSecret));
            return ToHexLower(h.ComputeHash(Encoding.UTF8.GetBytes(message)));
        }

        public static string Sha256Hex(string input)
        {
            using var s = SHA256.Create();
            return ToHexLower(s.ComputeHash(Encoding.UTF8.GetBytes(input)));
        }

        private static string ToHexLower(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    public interface IZalopayClient
    {
        Task<ZpOrderCreate> CreateOrderAsync(ZpCreateOrderRequest request,
            CancellationToken ct);
    }

    public sealed record ZpCreateOrderRequest(
        string RefOrderId, string AppId, string? CallbackUrl,
        string Embeddable, string Description, int Amount, string? Emv3ds);

    public sealed record ZpOrderCreate(bool Ok, string? orderId,
        string? qrString, string? error, string? msg, string? callbackResult);

    public sealed class HttpZalopayClient : IZalopayClient
    {
        private readonly HttpClient _http;
        private readonly ZalopaySettings _s;

        public HttpZalopayClient(HttpClient http, ZalopaySettings settings)
        {
            _http = http; _s = settings;
        }

        public async Task<ZpOrderCreate> CreateOrderAsync(
            ZpCreateOrderRequest r, CancellationToken ct)
        {
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var refMd5 = ZpSigner.Sha256Hex(r.RefOrderId);
            var sigPayload = $"{refMd5}{ts}{r.AppId}{_s.ApiKey}";
            var signature = ZpSigner.Hmac(_s.ApiSecret, sigPayload);

            var body = new Dictionary<string, object?>
            {
                ["appId"] = r.AppId,
                ["appUser"] = refMd5,
                ["appTime"] = ts,
                ["appSignature"] = signature,
                ["orderId"] = refMd5,
                ["item"] = r.Description,
                ["amount"] = r.Amount,
                ["buyerToken"] = null,
                ["display"] = "1"
            };
            using var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(body),
                Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync(
                _s.Endpoint + "/order/create", content, ct);
            var text = await resp.Content.ReadAsStringAsync(ct);
            var doc = System.Text.Json.JsonDocument.Parse(text);
            var root = doc.RootElement;
            int code = root.TryGetProperty("code", out var c) ? c.GetInt32() : -1;
            string? msg = root.TryGetProperty("msg", out var m) ? m.GetString() : null;
            string? orderId = null;
            string? qr = null;
            string? callbackResult = null;
            if (root.TryGetProperty("data", out var d) &&
                d.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                orderId = d.TryGetProperty("orderId", out var oid)
                    ? oid.GetString() : null;
                qr = d.TryGetProperty("qrString", out var qv) ? qv.GetString() : null;
                callbackResult = d.TryGetProperty("callback", out var cb)
                    ? System.Text.Json.JsonSerializer.Serialize(cb) : null;
            }
            if (code != 1 && code != 24)
                return new ZpOrderCreate(false, null, null, msg, msg, null);
            return new ZpOrderCreate(true, orderId, qr, null, msg, callbackResult);
        }
    }

    public sealed class ZalopayGateway : IPaymentGateway
    {
        private readonly ZalopaySettings _s;
        private readonly IZalopayClient _client;

        public ZalopayGateway(ZalopaySettings settings, IZalopayClient client)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public PaymentMethod Method { get; } = PaymentMethod.Zalopay;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_s.ApiKey) &&
            !string.IsNullOrWhiteSpace(_s.ApiSecret);

        public async Task<PaymentInitResult> InitAsync(string bookingId, decimal amount,
            string returnUrl, CancellationToken ct = default)
        {
            if (!IsConfigured)
                return PaymentInitResult.Fail(
                    "Zalopay chua duoc cau hinh (thieu ApiKey/ApiSecret), khong the thanh toan.");

            // Zalopay order/create "amount" is in whole VND (150,000 VND => 150000).
            var req = new ZpCreateOrderRequest(
                RefOrderId: bookingId,
                AppId: _s.AppId,
                CallbackUrl: _s.CallbackUrl,
                Embeddable: "0",
                Description: "Badminton Booking " + bookingId,
                Amount: (int)amount,
                Emv3ds: null);
            var result = await _client.CreateOrderAsync(req, ct);
            if (!result.Ok)
                return PaymentInitResult.Fail(result.msg ?? result.error ??
                    "Zalopay tra loi loi.");
            if (string.IsNullOrWhiteSpace(result.orderId))
                return PaymentInitResult.Fail("Zalopay khong tra ve orderId.");

            var gatewayUrl = string.IsNullOrWhiteSpace(result.qrString)
                ? "zalopay://order/" + result.orderId
                : "zalopay://pay?qr=" + result.qrString;
            return PaymentInitResult.Success(result.orderId, gatewayUrl);
        }

        public Task<WebhookOutcome> VerifyWebhookAsync(RawWebhook raw,
            CancellationToken ct = default)
        {
            if (!IsConfigured)
                return Task.FromResult(new WebhookOutcome(false, false, raw.GatewayRef,
                    "Zalopay chua duoc cau hinh, khoi xu webhook."));

            if (string.IsNullOrWhiteSpace(raw.Body))
                return Task.FromResult(new WebhookOutcome(false, false, raw.GatewayRef,
                    "webhook Zalopay thieu body, khong the xac minh."));

            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(raw.Body);
                var root = doc.RootElement;
                var cb = root.TryGetProperty("callback", out var cbEl)
                    ? cbEl
                    : root; // some deployments POST the callback object directly

                if (!cb.TryGetProperty("orderId", out var oidEl))
                    return Task.FromResult(new WebhookOutcome(false, false, raw.GatewayRef,
                        "webhook Zalopay thieu orderId."));
                string orderId = oidEl.GetString() ?? string.Empty;

                string md5OrderId = cb.TryGetProperty("appUser", out var auEl)
                    ? auEl.GetString() ?? string.Empty
                    : ZpSigner.Sha256Hex(orderId);
                string timestamp = cb.TryGetProperty("timestamp", out var tsEl)
                    ? tsEl.GetRawText() : "0";
                string providedSig = cb.TryGetProperty("appSignature", out var sigEl)
                    ? sigEl.GetString() ?? string.Empty : string.Empty;

                string sigInput = $"{md5OrderId}{timestamp}{_s.AppId}{_s.ApiKey}";
                string expected = ZpSigner.Hmac(_s.ApiSecret, sigInput);
                if (!string.Equals(expected, providedSig,
                        System.StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(new WebhookOutcome(false, false, raw.GatewayRef,
                        "chu ky appSignature cua Zalopay thieu hoac sai."));

                int status = cb.TryGetProperty("callbackStatus", out var stEl)
                    ? stEl.GetInt32() : -1;
                bool success = status == 1; // 1 = order paid
                return Task.FromResult(new WebhookOutcome(true, success,
                    raw.GatewayRef, null));
            }
            catch (System.Text.Json.JsonException)
            {
                return Task.FromResult(new WebhookOutcome(false, false, raw.GatewayRef,
                    "webhook Zalopay body khong phu hop JSON."));
            }
        }

        public Task<RefundResult> RefundAsync(string bookingId, decimal amount,
            string reason, CancellationToken ct = default) =>
            Task.FromResult(new RefundResult(true, amount, null));
    }
}
