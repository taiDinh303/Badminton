using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Badminton.Core.Domain;
using Badminton.Core.Enums;
using Badminton.Core.Store;
using Microsoft.Extensions.Options;
using Services;
using Xunit;

namespace Badminton.Tests;

public sealed class OnlineRefundProtocolTests
{
    private static OnlineRefundRequest Request(PaymentMethod method) => new(method, "REFUND1", "PAY1", "123456", "20261024120000", 50000, 100000, "Refund court", DateTime.UtcNow);
    private static HttpOnlineRefundClient Client(Handler handler, string zaloAppId = "1234") => new(new Factory(handler),
        Options.Create(new MoMoSettings { AppId = "partner", AccessKey = "access", SecretKey = "key" }),
        Options.Create(new ZalopaySettings { AppId = zaloAppId, ApiKey = "key" }),
        Options.Create(new VnPaySettings { MerchantCode = "merchant", SecretKey = "key" }));
    private static string Hash(string text, bool sha512 = false) => Convert.ToHexString(sha512
        ? HMACSHA512.HashData(Encoding.UTF8.GetBytes("key"), Encoding.UTF8.GetBytes(text))
        : HMACSHA256.HashData(Encoding.UTF8.GetBytes("key"), Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    [Fact]
    public async Task Momo_refund_uses_original_provider_id_and_signed_stable_refund_identity()
    {
        var handler = new Handler((uri, r) =>
        {
            Assert.EndsWith("/v2/gateway/api/refund", uri.AbsolutePath);
            Assert.Equal("123456", r["transId"]);
            Assert.Equal("REFUND1", r["requestId"]);
            Assert.Equal(Hash("accessKey=access&amount=50000&description=Refund court&orderId=REFUND1&partnerCode=partner&requestId=REFUND1&transId=123456"), r["signature"]);
            return JsonSerializer.Serialize(new { partnerCode = "partner", orderId = r["orderId"], requestId = r["requestId"], amount = 50000, resultCode = 0, transId = 999999 });
        });
        Assert.True((await Client(handler).ExecuteAsync(Request(PaymentMethod.MoMo), false)).Succeeded);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Momo_query_never_submits_and_mismatched_amount_cannot_complete_a_refund()
    {
        var handler = new Handler((uri, r) =>
        {
            Assert.EndsWith("/refund/query", uri.AbsolutePath);
            Assert.Equal(Hash($"accessKey=access&orderId=REFUND1&partnerCode=partner&requestId={r["requestId"]}"), r["signature"]);
            return JsonSerializer.Serialize(new { partnerCode = "partner", orderId = r["orderId"], requestId = r["requestId"], amount = 1, resultCode = 0, transId = 999999 });
        });
        Assert.False((await Client(handler).ExecuteAsync(Request(PaymentMethod.MoMo), true)).Succeeded);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Zalo_accepted_is_queried_and_processing_is_not_success()
    {
        var handler = new Handler((uri, r) =>
        {
            if (uri.AbsolutePath.EndsWith("/query_refund"))
            {
                Assert.Equal(Hash($"1234|REFUND1|{r["timestamp"]}"), r["mac"]);
                return "{\"return_code\":3}";
            }
            Assert.Equal(Hash($"REFUND1|123456|50000|Refund court|{r["timestamp"]}"), r["mac"]);
            return "{\"return_code\":1,\"refund_id\":9999}";
        });
        Assert.False((await Client(handler).ExecuteAsync(Request(PaymentMethod.Zalopay), false)).Succeeded);
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData("00", true)]
    [InlineData("05", false)]
    [InlineData("06", false)]
    [InlineData("09", false)]
    public async Task Vnpay_checks_response_mac_reference_amount_type_and_settlement(string status, bool success)
    {
        var handler = new Handler((uri, r) =>
        {
            Assert.Equal("/merchant_webapi/api/transaction", uri.AbsolutePath);
            Assert.Equal("03", r["vnp_TransactionType"]); Assert.Equal("5000000", r["vnp_Amount"]);
            var input = $"REFUND1|2.1.0|refund|merchant|03|PAY1|5000000|123456|20261024120000|Badminton|{r["vnp_CreateDate"]}|127.0.0.1|Refund court";
            Assert.Equal(Hash(input, true), r["vnp_SecureHash"]);
            var response = new Dictionary<string, string> { ["vnp_ResponseId"] = "R1", ["vnp_Command"] = "refund", ["vnp_ResponseCode"] = "00",
                ["vnp_Message"] = "OK", ["vnp_TmnCode"] = "merchant", ["vnp_TxnRef"] = "PAY1", ["vnp_Amount"] = "5000000",
                ["vnp_BankCode"] = "NCB", ["vnp_PayDate"] = "20261024130000", ["vnp_TransactionNo"] = "999999", ["vnp_TransactionType"] = "03",
                ["vnp_TransactionStatus"] = status, ["vnp_OrderInfo"] = "Refund court" };
            response["vnp_SecureHash"] = Hash($"R1|refund|00|OK|merchant|PAY1|5000000|NCB|20261024130000|999999|03|{status}|Refund court", true);
            return JsonSerializer.Serialize(response);
        });
        Assert.Equal(success, (await Client(handler).ExecuteAsync(Request(PaymentMethod.VnPay), false)).Succeeded);
    }

    [Fact]
    public async Task Vnpay_forged_or_unsigned_refund_response_is_not_success()
    {
        var handler = new Handler((_, _) => "{\"vnp_ResponseCode\":\"00\",\"vnp_TransactionStatus\":\"00\"}");
        Assert.False((await Client(handler).ExecuteAsync(Request(PaymentMethod.VnPay), false)).Succeeded);
    }

    [Fact]
    public async Task Invalid_zalo_merchant_configuration_never_issues_a_refund()
    {
        var handler = new Handler((_, _) => throw new InvalidOperationException("No network for invalid merchant metadata."));
        Assert.False((await Client(handler, "invalid-merchant").ExecuteAsync(Request(PaymentMethod.Zalopay), false)).Succeeded);
        Assert.Equal(0, handler.Calls);
    }

    private sealed class Factory(Handler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
    private sealed class Handler(Func<Uri, Dictionary<string, string>, string> response) : HttpMessageHandler
    {
        public int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var fields = doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText());
            return new(HttpStatusCode.OK) { Content = new StringContent(response(request.RequestUri!, fields), Encoding.UTF8, "application/json") };
        }
    }
}
