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
            using var h = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret));
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
        string Embeddable, string Description, int Amount, string? Emv3ds, string? ReturnUrl = null);

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
            var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var embed = System.Text.Json.JsonSerializer.Serialize(new { redirecturl = r.ReturnUrl });
            const string item = "[]";
            const string appUser = "BadmintonBooking";
            var mac = ZpSigner.Hmac(_s.ApiKey, $"{r.AppId}|{r.RefOrderId}|{appUser}|{r.Amount}|{ts}|{embed}|{item}");
            using var content = new FormUrlEncodedContent(new Dictionary<string, string> {
                ["app_id"] = r.AppId, ["app_user"] = appUser, ["app_trans_id"] = r.RefOrderId,
                ["app_time"] = ts.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["amount"] = r.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["embed_data"] = embed, ["item"] = item, ["mac"] = mac,
                ["description"] = r.Description, ["callback_url"] = r.CallbackUrl ?? "", ["bank_code"] = ""
            });
            using var resp = await _http.PostAsync(_s.Endpoint.TrimEnd('/') + "/v2/create", content, ct);
            if (!resp.IsSuccessStatusCode) return new(false, null, null, "HTTP_ERROR", "ZaloPay không xác nhận khởi tạo giao dịch.", null);
            using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            if (!root.TryGetProperty("return_code", out var code) || !code.TryGetInt32(out var result) || result != 1
                || !root.TryGetProperty("order_url", out var url) || url.ValueKind != System.Text.Json.JsonValueKind.String
                || !Uri.TryCreate(url.GetString(), UriKind.Absolute, out var checkout) || checkout.Scheme != "https")
                return new(false, null, null, "PROVIDER_REJECTED", "ZaloPay không trả checkout hợp lệ.", null);
            return new(true, r.RefOrderId, checkout.AbsoluteUri, null, null, null);
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
            !string.IsNullOrWhiteSpace(_s.AppId);

        public async Task<PaymentInitResult> InitAsync(string bookingId, decimal amount,
            string returnUrl, CancellationToken ct = default)
        {
            if (!IsConfigured)
                return PaymentInitResult.Fail(
                    "Zalopay chua duoc cau hinh (thieu ApiKey/ApiSecret), khong the thanh toan.");

            if (amount <= 0 || amount != decimal.Truncate(amount) || amount > int.MaxValue)
                return PaymentInitResult.Fail("Số tiền ZaloPay không hợp lệ.");
            var req = new ZpCreateOrderRequest(bookingId, _s.AppId, _s.CallbackUrl,
                "0", "Badminton Booking " + bookingId, (int)amount, null, returnUrl);
            var result = await _client.CreateOrderAsync(req, ct);
            if (!result.Ok || result.orderId != bookingId || string.IsNullOrWhiteSpace(result.qrString))
                return PaymentInitResult.Fail("ZaloPay không xác nhận đúng giao dịch hoặc thiếu checkout.");
            return PaymentInitResult.Success(bookingId, result.qrString);
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
                if (string.IsNullOrWhiteSpace(_s.CallbackKey))
                    return Task.FromResult(new WebhookOutcome(false, false, null, "ZaloPay thiếu CallbackKey."));
                using var envelope = System.Text.Json.JsonDocument.Parse(raw.Body);
                var root = envelope.RootElement;
                if (root.ValueKind != System.Text.Json.JsonValueKind.Object || root.EnumerateObject().Select(p => p.Name).Distinct().Count() != root.EnumerateObject().Count()
                    || !root.TryGetProperty("type", out var type) || type.ValueKind != System.Text.Json.JsonValueKind.Number || !type.TryGetInt32(out var kind) || kind != 1
                    || !root.TryGetProperty("data", out var data) || data.ValueKind != System.Text.Json.JsonValueKind.String
                    || !root.TryGetProperty("mac", out var mac) || mac.ValueKind != System.Text.Json.JsonValueKind.String)
                    return Task.FromResult(new WebhookOutcome(false, false, null, "Webhook ZaloPay thiếu data/mac/type."));
                var signedData = data.GetString()!;
                var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(_s.CallbackKey), Encoding.UTF8.GetBytes(signedData)));
                if (!WebhookSecurity.EqualHex(mac.GetString(), expected))
                    return Task.FromResult(new WebhookOutcome(false, false, null, "Chữ ký ZaloPay không hợp lệ."));
                var fields = WebhookSecurity.ReadObject(signedData);
                if (!fields.TryGetValue("app_trans_id", out var reference) || reference != raw.GatewayRef
                    || !fields.TryGetValue("app_id", out var app) || app != _s.AppId
                    || !fields.TryGetValue("amount", out var rawAmount) || !long.TryParse(rawAmount, System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount < 0)
                    return Task.FromResult(new WebhookOutcome(false, false, null, "Giao dịch ZaloPay không khớp ứng dụng/đơn/số tiền."));
                return Task.FromResult(new WebhookOutcome(true, true, reference, null, amount, "VND", fields.GetValueOrDefault("zp_trans_id")));
            }
            catch (System.Text.Json.JsonException)
            {
                return Task.FromResult(new WebhookOutcome(false, false, null, "Webhook ZaloPay không hợp lệ."));
            }
        }

        public Task<RefundResult> RefundAsync(string bookingId, decimal amount,
            string reason, CancellationToken ct = default) =>
            Task.FromResult(RefundResult.Fail("ONLINE_REFUND_UNAVAILABLE: Chưa có kết nối hoàn tiền online. Giao dịch chưa được hoàn; cần xử lý và đối soát với nhà cung cấp."));
    }
}
