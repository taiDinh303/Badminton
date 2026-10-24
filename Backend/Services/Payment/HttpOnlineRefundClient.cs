using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Badminton.Core.Domain;
using Badminton.Core.Enums;
using Badminton.Core.Store;
using Badminton.Core.Utils;
using Microsoft.Extensions.Options;

namespace Services;

public sealed class HttpOnlineRefundClient(IHttpClientFactory clients, IOptions<MoMoSettings> momo,
    IOptions<ZalopaySettings> zalo, IOptions<VnPaySettings> vnp) : IOnlineRefundClient
{
    public string CreateRequestId(PaymentMethod method) => method == PaymentMethod.Zalopay
        ? $"{HanoiTime.HanoiNow():yyMMdd}_{zalo.Value.AppId}_{Guid.NewGuid():N}"[..Math.Min(45, 8 + zalo.Value.AppId.Length + 32)]
        : Guid.NewGuid().ToString("N");

    public async Task<OnlineRefundOutcome> ExecuteAsync(OnlineRefundRequest request, bool queryOnly, CancellationToken ct = default)
    {
        if (request.Amount <= 0 || request.Amount > request.OriginalAmount || string.IsNullOrWhiteSpace(request.TransactionId))
            return OnlineRefundOutcome.Pending("invalid_refund_metadata");
        return request.Method switch
        {
            PaymentMethod.MoMo => await MomoAsync(request, queryOnly, ct),
            PaymentMethod.Zalopay => await ZaloAsync(request, queryOnly, ct),
            PaymentMethod.VnPay => await VnpAsync(request, queryOnly, ct),
            _ => OnlineRefundOutcome.Pending("unsupported_refund_provider")
        };
    }

    private async Task<OnlineRefundOutcome> MomoAsync(OnlineRefundRequest r, bool query, CancellationToken ct)
    {
        var s = momo.Value;
        var fields = new Dictionary<string, object> { ["partnerCode"] = s.AppId, ["orderId"] = r.RequestId,
            ["requestId"] = Guid.NewGuid().ToString("N"), ["lang"] = "vi" };
        string canonical;
        if (query) canonical = $"accessKey={s.AccessKey}&orderId={r.RequestId}&partnerCode={s.AppId}&requestId={fields["requestId"]}";
        else
        {
            if (!long.TryParse(r.TransactionId, out var transactionId)) return OnlineRefundOutcome.Pending("missing_provider_transaction");
            fields["requestId"] = r.RequestId; // stable request AND refund order ID for submission
            fields["amount"] = r.Amount; fields["transId"] = transactionId; fields["description"] = r.Reason;
            canonical = $"accessKey={s.AccessKey}&amount={r.Amount}&description={r.Reason}&orderId={r.RequestId}&partnerCode={s.AppId}&requestId={r.RequestId}&transId={r.TransactionId}";
        }
        fields["signature"] = Sign(s.SecretKey, canonical);
        var result = await PostAsync(s.Endpoint.TrimEnd('/') + "/v2/gateway/api/refund" + (query ? "/query" : ""), fields, ct);
        if (result is null) return OnlineRefundOutcome.Pending();
        var d = result;

        var success = d.GetValueOrDefault("partnerCode") == s.AppId && d.GetValueOrDefault("orderId") == r.RequestId
            && d.GetValueOrDefault("requestId") == fields["requestId"].ToString()
            && d.GetValueOrDefault("amount") == r.Amount.ToString(CultureInfo.InvariantCulture)
            && d.GetValueOrDefault("resultCode") == "0" && long.TryParse(d.GetValueOrDefault("transId"), out var refundId) && refundId > 0;
        return new(success, success ? "refund_succeeded" : "refund_uncertain");
    }

    private async Task<OnlineRefundOutcome> ZaloAsync(OnlineRefundRequest r, bool query, CancellationToken ct)
    {
        var s = zalo.Value;
        if (!int.TryParse(s.AppId, NumberStyles.None, CultureInfo.InvariantCulture, out var appId) || appId <= 0)
            return OnlineRefundOutcome.Pending("invalid_refund_configuration");
        var stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var f = new Dictionary<string, object> { ["app_id"] = appId, ["m_refund_id"] = r.RequestId, ["timestamp"] = stamp };
        var canonical = $"{s.AppId}|{r.RequestId}|{stamp}";
        if (!query)
        {
            if (!long.TryParse(r.TransactionId, out var transactionId)) return OnlineRefundOutcome.Pending("missing_provider_transaction");
            f["zp_trans_id"] = transactionId; f["amount"] = r.Amount; f["description"] = r.Reason;
            canonical = $"{r.RequestId}|{r.TransactionId}|{r.Amount}|{r.Reason}|{stamp}";
        }
        f["mac"] = Sign(s.ApiKey, canonical);
        var d = await PostAsync(s.Endpoint.TrimEnd('/') + (query ? "/v2/query_refund" : "/v2/refund"), f, ct);
        if (d is null) return OnlineRefundOutcome.Pending();
        // refund return_code=1 means accepted. Query is required to prove settlement.
        if (!query && d.GetValueOrDefault("return_code") == "1") return await ZaloAsync(r, true, ct);
        return new(query && d.GetValueOrDefault("return_code") == "1", "refund_uncertain");
    }

    private async Task<OnlineRefundOutcome> VnpAsync(OnlineRefundRequest r, bool query, CancellationToken ct)
    {
        var s = vnp.Value;
        if (r.TransactionDate.Length != 14 || !r.TransactionDate.All(char.IsAsciiDigit))
            return OnlineRefundOutcome.Pending("missing_provider_transaction_date");
        var command = query ? "querydr" : "refund";
        var type = r.Amount == r.OriginalAmount ? "02" : "03";
        var f = new Dictionary<string, object> { ["vnp_RequestId"] = query ? Guid.NewGuid().ToString("N") : r.RequestId,
            ["vnp_Version"] = s.Version, ["vnp_Command"] = command, ["vnp_TmnCode"] = s.MerchantCode,
            ["vnp_TxnRef"] = r.PaymentReference, ["vnp_TransactionNo"] = r.TransactionId,
            ["vnp_TransactionDate"] = r.TransactionDate, ["vnp_CreateDate"] = HanoiTime.HanoiNow().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_IpAddr"] = "127.0.0.1", ["vnp_OrderInfo"] = r.Reason };
        string[] keys;
        if (query) keys = ["vnp_RequestId", "vnp_Version", "vnp_Command", "vnp_TmnCode", "vnp_TxnRef", "vnp_TransactionDate", "vnp_CreateDate", "vnp_IpAddr", "vnp_OrderInfo"];
        else
        {
            f["vnp_TransactionType"] = type; f["vnp_Amount"] = ((long)r.Amount * 100).ToString(CultureInfo.InvariantCulture); f["vnp_CreateBy"] = "Badminton";
            keys = ["vnp_RequestId", "vnp_Version", "vnp_Command", "vnp_TmnCode", "vnp_TransactionType", "vnp_TxnRef", "vnp_Amount", "vnp_TransactionNo", "vnp_TransactionDate", "vnp_CreateBy", "vnp_CreateDate", "vnp_IpAddr", "vnp_OrderInfo"];
        }
        f["vnp_SecureHash"] = Sign(s.SecretKey, string.Join("|", keys.Select(k => f[k])), sha512: true);
        var d = await PostAsync(s.ApiEndpoint, f, ct);
        if (d is null) return OnlineRefundOutcome.Pending();
        var responseKeys = new List<string> { "vnp_ResponseId", "vnp_Command", "vnp_ResponseCode", "vnp_Message", "vnp_TmnCode", "vnp_TxnRef", "vnp_Amount", "vnp_BankCode", "vnp_PayDate", "vnp_TransactionNo", "vnp_TransactionType", "vnp_TransactionStatus", "vnp_OrderInfo" };
        if (query) responseKeys.AddRange(["vnp_PromotionCode", "vnp_PromotionAmount"]);
        var expected = Sign(s.SecretKey, string.Join("|", responseKeys.Select(k => d.GetValueOrDefault(k) ?? "")), sha512: true);
        var amount = ((long)r.Amount * 100).ToString(CultureInfo.InvariantCulture);
        var verified = WebhookSecurity.EqualHex(d.GetValueOrDefault("vnp_SecureHash"), expected)
            && d.GetValueOrDefault("vnp_Command") == command && d.GetValueOrDefault("vnp_TmnCode") == s.MerchantCode
            && d.GetValueOrDefault("vnp_TxnRef") == r.PaymentReference && d.GetValueOrDefault("vnp_Amount") == amount
            && d.GetValueOrDefault("vnp_TransactionType") == type && d.GetValueOrDefault("vnp_ResponseCode") == "00";
        return new(verified && d.GetValueOrDefault("vnp_TransactionStatus") == "00", "refund_uncertain");
    }

    private async Task<Dictionary<string, string>?> PostAsync(string endpoint, Dictionary<string, object> fields, CancellationToken ct)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var url) || url.Scheme != "https" || !string.IsNullOrEmpty(url.UserInfo))
            return null;
        using var http = clients.CreateClient("online-refund");
        using var response = await http.PostAsJsonAsync(url, fields, ct);
        if (!response.IsSuccessStatusCode) return null;
        var body = await response.Content.ReadAsStringAsync(ct);
        return body.Length > 65536 ? null : WebhookSecurity.ReadObject(body);
    }
    private static string Sign(string key, string value, bool sha512 = false) => Convert.ToHexString(sha512
        ? HMACSHA512.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(value))
        : HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
