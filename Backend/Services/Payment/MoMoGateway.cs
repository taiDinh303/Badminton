using System.Security.Cryptography;
using System.Text;
using Badminton.Core.Domain;
using Badminton.Core.Enums;
using Badminton.Core.Store;

namespace Services
{
    public static class MoMoSigner
    {
        public static string RequestId(long timestampMillis)
        {
            using var md5 = MD5.Create();
            return ToHexLower(md5.ComputeHash(Encoding.UTF8.GetBytes(timestampMillis.ToString())));
        }

        public static string AccessKey(string requestId, string secretKey) =>
            ToHexLower(HmacSha512(secretKey, requestId));

        public static string ComputeSign(IEnumerable<KeyValuePair<string, string>> paramsDict)
        {
            var ordered = paramsDict
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal);
            var sb = new StringBuilder();
            bool first = true;
            foreach (var kv in ordered)
            {
                if (kv.Key is "sign" or "accessKey") continue;
                if (string.IsNullOrEmpty(kv.Value)) continue;
                if (!first) sb.Append('&');
                sb.Append(kv.Key).Append('=').Append(kv.Value);
                first = false;
            }
            return ToHexLower(Sha512(Encoding.UTF8.GetBytes(sb.ToString())));
        }

        public static string ResponseKey(string orderId, string secretKey) =>
            ToHexLower(HmacSha512(secretKey, orderId));

        public static string Sha512Hex(string input) =>
            ToHexLower(Sha512(Encoding.UTF8.GetBytes(input)));

        private static byte[] HmacSha512(string key, string message)
        {
            using var h = new HMACSHA512(Encoding.UTF8.GetBytes(key));
            return h.ComputeHash(Encoding.UTF8.GetBytes(message));
        }

        private static byte[] Sha512(byte[] data)
        {
            using var s = SHA512.Create();
            return s.ComputeHash(data);
        }

        private static string ToHexLower(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    public interface IMomoApiClient
    {
        Task<MomoPayInit> CreatePayUrlAsync(MomoPayRequest request, CancellationToken ct);
    }

    public sealed record MomoPayRequest(string PartnerCode, string OrderId, long Amount,
        string OrderInfo, string RedirectUrl, string IpnUrl, string RequestId,
        string Signature, string RequestType = "captureWallet", string ExtraData = "");

    public sealed record MomoPayInit(bool Ok, string? CreatePayUrl, string? OrderId,
        string? ErrorCode, string? Message);

    public sealed class HttpMoMoApiClient : IMomoApiClient
    {
        private readonly HttpClient _http;
        private readonly MoMoSettings _settings;
        private readonly MomoPayMethod _payMethod;

        public HttpMoMoApiClient(HttpClient http, MoMoSettings settings, MomoPayMethod payMethod)
        {
            _http = http; _settings = settings; _payMethod = payMethod;
        }

        public async Task<MomoPayInit> CreatePayUrlAsync(MomoPayRequest r, CancellationToken ct)
        {
            using var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new {
                partnerCode = r.PartnerCode, requestId = r.RequestId, orderId = r.OrderId,
                amount = r.Amount, orderInfo = r.OrderInfo, redirectUrl = r.RedirectUrl,
                ipnUrl = r.IpnUrl, requestType = r.RequestType, extraData = r.ExtraData,
                signature = r.Signature, lang = "vi", autoCapture = true
            }), Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync(_settings.Endpoint.TrimEnd('/') + "/v2/gateway/api/create", content, ct);
            if (!resp.IsSuccessStatusCode) return new(false, null, null, "HTTP_ERROR", "MoMo không xác nhận khởi tạo giao dịch.");
            using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            if (!root.TryGetProperty("resultCode", out var code) || !code.TryGetInt32(out var result) || result != 0)
                return new(false, null, null, "PROVIDER_REJECTED", "MoMo từ chối khởi tạo giao dịch.");
            if (!root.TryGetProperty("orderId", out var order) || order.GetString() != r.OrderId
                || !root.TryGetProperty("requestId", out var request) || request.GetString() != r.RequestId
                || !root.TryGetProperty("amount", out var amount) || !amount.TryGetInt64(out var actualAmount) || actualAmount != r.Amount
                || !root.TryGetProperty("payUrl", out var url) || url.ValueKind != System.Text.Json.JsonValueKind.String
                || !Uri.TryCreate(url.GetString(), UriKind.Absolute, out var checkout) || checkout.Scheme != "https")
                return new(false, null, null, "INVALID_RESPONSE", "Phản hồi MoMo không khớp giao dịch.");
            var signedFields = WebhookSecurity.ReadObject(root.GetRawText());
            string[] responseKeys = ["amount", "message", "orderId", "partnerCode", "payUrl", "requestId", "responseTime", "resultCode"];
            if (responseKeys.Any(k => !signedFields.ContainsKey(k)) || signedFields["partnerCode"] != r.PartnerCode
                || !signedFields.TryGetValue("signature", out var signature))
                return new(false, null, null, "INVALID_RESPONSE", "MoMo thiếu dữ liệu xác minh phản hồi.");
            var canonical = "accessKey=" + _settings.AccessKey + "&" + string.Join("&", responseKeys.Select(k => k + "=" + signedFields[k]));
            var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(_settings.SecretKey), Encoding.UTF8.GetBytes(canonical)));
            if (!WebhookSecurity.EqualHex(signature, expected)) return new(false, null, null, "INVALID_RESPONSE", "Chữ ký phản hồi MoMo không hợp lệ.");
            return new(true, checkout.AbsoluteUri, r.OrderId, null, null);
        }
    }

    public sealed class MoMoGateway : IPaymentGateway
    {
        private readonly MoMoSettings _s;
        private readonly IMomoApiClient _client;

        public MoMoGateway(MoMoSettings settings, IMomoApiClient client)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public PaymentMethod Method { get; } = PaymentMethod.MoMo;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_s.AppId) && !string.IsNullOrWhiteSpace(_s.SecretKey);

        public async Task<PaymentInitResult> InitAsync(string bookingId, decimal amount,
            string returnUrl, CancellationToken ct = default)
        {
            if (!IsConfigured || string.IsNullOrWhiteSpace(_s.AccessKey) || string.IsNullOrWhiteSpace(_s.IpayUrl))
                return PaymentInitResult.Fail("MoMo chua duoc cau hinh (AppId/AccessKey/SecretKey/IpayUrl).");
            if (amount != decimal.Truncate(amount) || amount < 1000 || amount > 50000000)
                return PaymentInitResult.Fail("Số tiền nằm ngoài giới hạn ví MoMo.");
            var orderInfo = "Badminton Booking " + bookingId;
            var requestId = bookingId;
            var canonical = $"accessKey={_s.AccessKey}&amount={amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}&extraData=&ipnUrl={_s.IpayUrl}&orderId={bookingId}&orderInfo={orderInfo}&partnerCode={_s.AppId}&redirectUrl={returnUrl}&requestId={requestId}&requestType=captureWallet";
            var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(_s.SecretKey), Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
            var init = await _client.CreatePayUrlAsync(new(_s.AppId, bookingId, (long)amount,
                orderInfo, returnUrl, _s.IpayUrl, requestId, signature), ct);
            if (!init.Ok || string.IsNullOrWhiteSpace(init.CreatePayUrl))
                return PaymentInitResult.Fail("MoMo không xác nhận khởi tạo giao dịch.");
            if (init.OrderId != bookingId) return PaymentInitResult.Fail("MoMo trả về mã giao dịch không khớp.");
            return PaymentInitResult.Success(bookingId, init.CreatePayUrl);
        }

        public Task<WebhookOutcome> VerifyWebhookAsync(RawWebhook raw, CancellationToken ct = default)
        {
            if (!IsConfigured)
                return Task.FromResult(new WebhookOutcome(false, false, raw.GatewayRef,
                    "MoMo chua duoc cau hinh, khoi xu webhook."));

            if (string.IsNullOrWhiteSpace(raw.Body))
                return Task.FromResult(new WebhookOutcome(false, false, raw.GatewayRef,
                    "webhook MoMo thieu body, khong the xac minh."));

            // MoMo v2 IPN: every settlement field belongs to the HMAC payload.
            try
            {
                if (string.IsNullOrWhiteSpace(_s.AccessKey))
                    return Task.FromResult(new WebhookOutcome(false, false, null, "MoMo thiếu AccessKey cho IPN."));
                var fields = WebhookSecurity.ReadObject(raw.Body);
                string[] keys = ["amount", "extraData", "message", "orderId", "orderInfo", "orderType", "partnerCode", "payType", "requestId", "responseTime", "resultCode", "transId"];
                if (keys.Any(k => !fields.ContainsKey(k)) || !fields.TryGetValue("signature", out var signature))
                    return Task.FromResult(new WebhookOutcome(false, false, null, "Webhook MoMo thiếu dữ liệu ký."));
                var canonical = "accessKey=" + _s.AccessKey + "&" + string.Join("&", keys.Select(k => k + "=" + fields[k]));
                var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(_s.SecretKey), Encoding.UTF8.GetBytes(canonical)));
                if (!WebhookSecurity.EqualHex(signature, expected) || fields["partnerCode"] != _s.AppId
                    || fields["orderId"] != raw.GatewayRef || !long.TryParse(fields["amount"], System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount < 0)
                    return Task.FromResult(new WebhookOutcome(false, false, null, "Chữ ký hoặc giao dịch MoMo không hợp lệ."));
                return Task.FromResult(new WebhookOutcome(true, fields["resultCode"] == "0", fields["orderId"], null, amount, "VND", fields.GetValueOrDefault("transId")));
            }
            catch (System.Text.Json.JsonException)
            {
                return Task.FromResult(new WebhookOutcome(false, false, null, "Webhook MoMo không hợp lệ."));
            }
        }

        public Task<RefundResult> RefundAsync(string bookingId, decimal amount,
            string reason, CancellationToken ct = default)
        {
            return Task.FromResult(RefundResult.Fail("ONLINE_REFUND_UNAVAILABLE: Chưa có kết nối hoàn tiền online. Giao dịch chưa được hoàn; cần xử lý và đối soát với nhà cung cấp."));
        }
    }

    public enum MomoPayMethod
    {
        momo_wallet = 1,
        qr_code = 2
    }
}
