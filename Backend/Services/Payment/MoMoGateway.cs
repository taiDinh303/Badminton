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

    public sealed record MomoPayRequest(
        string AppId, string PartnerCode, string PartnerRef,
        long Timestamp, string IpAddr, int Amount,
        string OrderInfo, string ReturnUrl,
        string RequestId, string AccessKey, string Sign,
        int PayMethod);

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
            var payload = new Dictionary<string, string>
            {
                ["partnerCode"] = r.PartnerCode,
                ["partnerRef"] = r.PartnerRef,
                ["merchantRef"] = r.PartnerRef,
                ["orderId"] = r.PartnerRef,
                ["orderInfo"] = r.OrderInfo,
                ["amount"] = r.Amount.ToString(),
                ["returnUrl"] = r.ReturnUrl,
                ["cancelUrl"] = r.ReturnUrl,
                ["userId"] = r.AppId,
                ["payMethod"] = r.PayMethod.ToString(),
                ["requestId"] = r.RequestId,
                ["timestamp"] = r.Timestamp.ToString(),
                ["sign"] = r.Sign
            };
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync(
                _settings.Endpoint + "/gateway/api/" + _payMethod + "/createpayurl", content, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            var doc = System.Text.Json.JsonDocument.Parse(body);
            var code = doc.RootElement.TryGetProperty("resultCode", out var rc) ? rc.GetInt32() : -1;
            var msg = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
            if (code != 0)
                return new MomoPayInit(false, null, null, msg, msg);
            var payUrl = doc.RootElement.TryGetProperty("createPayUrl", out var pu)
                ? pu.GetString() : null;
            var orderId = doc.RootElement.TryGetProperty("orderId", out var oid)
                ? oid.GetString() : null;
            return new MomoPayInit(true, payUrl, orderId, null, null);
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

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_s.SecretKey);

        public Task<PaymentInitResult> InitAsync(string bookingId, decimal amount,
            string returnUrl, CancellationToken ct = default)
        {
            if (!IsConfigured)
                return Task.FromResult(PaymentInitResult.Fail(
                    "MoMo chua duoc cau hinh (thieu SecretKey), khong the thanh toan truc tuyen."));

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var requestId = MoMoSigner.RequestId(timestamp * 1000);
            var accessKey = MoMoSigner.AccessKey(requestId, _s.SecretKey);

            var paramsDict = new Dictionary<string, string>
            {
                ["partnerCode"] = _s.AppId,
                ["partnerRef"] = bookingId,
                ["orderInfo"] = "Badminton Booking " + bookingId,
                ["amount"] = ((int)amount).ToString(),
                ["returnUrl"] = returnUrl,
                ["cancelUrl"] = returnUrl,
                ["requestId"] = requestId,
                ["timestamp"] = timestamp.ToString()
            };
            var sign = MoMoSigner.ComputeSign(paramsDict);

            var req = new MomoPayRequest(
                _s.AppId, _s.AppId, bookingId, timestamp,
                "127.0.0.1", (int)amount, "Badminton Booking " + bookingId,
                returnUrl, requestId, accessKey, sign, 1);

            return CreatePay(req, ct);
        }

        private async Task<PaymentInitResult> CreatePay(MomoPayRequest req, CancellationToken ct)
        {
            try
            {
                var init = await _client.CreatePayUrlAsync(req, ct);
                if (!init.Ok)
                    return PaymentInitResult.Fail(init.Message ?? "MoMo tra loi loi.");
                if (string.IsNullOrEmpty(init.CreatePayUrl))
                    return PaymentInitResult.Fail("MoMo khong tra ve payUrl.");
                return PaymentInitResult.Success(init.OrderId ?? req.PartnerRef, init.CreatePayUrl);
            }
            catch (Exception ex)
            {
                return PaymentInitResult.Fail("Loi goi MoMo: " + ex.Message);
            }
        }

        public Task<WebhookOutcome> VerifyWebhookAsync(RawWebhook raw, CancellationToken ct = default)
        {
            if (!IsConfigured)
                return Task.FromResult(new WebhookOutcome(false, false, raw.GatewayRef,
                    "MoMo chua duoc cau hinh, khoi xu webhook."));

            if (string.IsNullOrWhiteSpace(raw.Body))
                return Task.FromResult(new WebhookOutcome(false, false, raw.GatewayRef,
                    "webhook MoMo thieu body, khong the xac minh."));

            // Parse the notification body for the order id + provider responseKey.
            string? orderId = null;
            string? providerKey = null;
            string? status = null;
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(raw.Body);
                orderId = doc.RootElement.TryGetProperty("orderId", out var o) ? o.GetString() : null;
                providerKey = doc.RootElement.TryGetProperty("responseKey", out var r) ? r.GetString() : null;
                status = doc.RootElement.TryGetProperty("responseStatus", out var s) ? s.GetString() : null;
            }
            catch (System.Text.Json.JsonException)
            {
                return Task.FromResult(new WebhookOutcome(false, false, raw.GatewayRef,
                    "webhook MoMo body khong phu hop JSON."));
            }

            if (string.IsNullOrEmpty(orderId))
                return Task.FromResult(new WebhookOutcome(false, false, raw.GatewayRef,
                    "webhook MoMo thieu orderId de xac minh."));

            if (!string.IsNullOrWhiteSpace(raw.GatewayRef)
                && !string.Equals(raw.GatewayRef, orderId, System.StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new WebhookOutcome(false, false, raw.GatewayRef,
                    "MoMo orderId khong khop voi gateway_ref (nguon goc nghich nhau)."));

            var expectedKey = MoMoSigner.ResponseKey(orderId, _s.SecretKey);
            if (string.IsNullOrWhiteSpace(providerKey)
                || !string.Equals(providerKey, expectedKey, System.StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new WebhookOutcome(false, false, raw.GatewayRef,
                    "chu ky responseKey cua MoMo thieu hoac sai (khong the xac minh)."));

            var success = status == "0";

            return Task.FromResult(new WebhookOutcome(true, success, raw.GatewayRef, null));
        }

        public Task<RefundResult> RefundAsync(string bookingId, decimal amount,
            string reason, CancellationToken ct = default)
        {
            return Task.FromResult(new RefundResult(true, amount, null));
        }
    }

    public enum MomoPayMethod
    {
        momo_wallet = 1,
        qr_code = 2
    }
}
