using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Badminton.Core.Store;
using Services;
using Xunit;

namespace Badminton.Tests;

public sealed class PaymentProtocolTests
{
    [Fact]
    public async Task Momo_v2_create_signs_complete_fields_and_accepts_matching_checkout()
    {
        var handler = new CaptureHandler((request, body) => {
            Assert.Equal("https://test-payment.momo.vn/v2/gateway/api/create", request.RequestUri!.AbsoluteUri);
            using var doc = JsonDocument.Parse(body); var r = doc.RootElement;
            Assert.Equal("captureWallet", r.GetProperty("requestType").GetString());
            Assert.Equal(150000, r.GetProperty("amount").GetInt64());
            Assert.Equal("ORDER", r.GetProperty("orderId").GetString());
            const string signed = "accessKey=access&amount=150000&extraData=&ipnUrl=https://api.example.test/callback&orderId=ORDER&orderInfo=Badminton Booking ORDER&partnerCode=partner&redirectUrl=https://web.example.test/return&requestId=ORDER&requestType=captureWallet";
            var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("key"), Encoding.UTF8.GetBytes(signed))).ToLowerInvariant();
            Assert.Equal(expected, r.GetProperty("signature").GetString());
            Assert.False(r.TryGetProperty("partnerRef", out _));
            const string responseData = "accessKey=access&amount=150000&message=OK&orderId=ORDER&partnerCode=partner&payUrl=https://test-payment.momo.vn/pay&requestId=ORDER&responseTime=1735689600000&resultCode=0";
            var responseMac = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("key"), Encoding.UTF8.GetBytes(responseData))).ToLowerInvariant();
            return JsonSerializer.Serialize(new { resultCode = 0, orderId = "ORDER", requestId = "ORDER", partnerCode = "partner", amount = 150000,
                responseTime = 1735689600000L, message = "OK", payUrl = "https://test-payment.momo.vn/pay", signature = responseMac });
        });
        var settings = new MoMoSettings { AppId = "partner", SecretKey = "key", AccessKey = "access", IpayUrl = "https://api.example.test/callback" };
        using var http = new HttpClient(handler);
        var result = await new MoMoGateway(settings, new HttpMoMoApiClient(http, settings, MomoPayMethod.momo_wallet)).InitAsync("ORDER", 150000, "https://web.example.test/return");
        Assert.True(result.Ok); Assert.Equal("ORDER", result.GatewayRef); Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("{\"resultCode\":0,\"orderId\":\"OTHER\",\"requestId\":\"ORDER\",\"amount\":150000,\"payUrl\":\"https://checkout.example.test\"}")]
    [InlineData("{\"resultCode\":0,\"orderId\":\"ORDER\",\"requestId\":\"ORDER\",\"amount\":1,\"payUrl\":\"https://checkout.example.test\"}")]
    [InlineData("{\"resultCode\":99,\"message\":\"synthetic-secret-must-not-leak\"}")]
    public async Task Momo_rejects_mismatched_or_failed_responses_without_echoing_provider_text(string response)
    {
        using var http = new HttpClient(new CaptureHandler((_, _) => response));
        var result = await new HttpMoMoApiClient(http, new MoMoSettings(), MomoPayMethod.momo_wallet)
            .CreatePayUrlAsync(new("partner", "ORDER", 150000, "info", "https://web.example.test", "https://api.example.test", "ORDER", "sig"), default);
        Assert.False(result.Ok); Assert.DoesNotContain("synthetic-secret", result.Message);
    }

    [Fact]
    public async Task Vnpay_generates_signed_redirect_locally_with_correct_v21_fields_and_reference()
    {
        var handler = new CaptureHandler((_, _) => throw new InvalidOperationException("VNPAY checkout must never be fetched by the backend."));
        using var http = new HttpClient(handler);
        var settings = new VnPaySettings { MerchantCode = "merchant", SecretKey = "key" };
        var result = await new VnPayGateway(settings, new HttpVnPayClient(http, settings)).InitAsync("ORDER", 150000, "https://web.example.test/return");
        Assert.True(result.Ok); Assert.Equal(0, handler.Calls); Assert.Equal("ORDER", result.GatewayRef);
        var fields = System.Web.HttpUtility.ParseQueryString(new Uri(result.GatewayUrl!).Query);
        Assert.Equal("2.1.0", fields["vnp_Version"]); Assert.Equal("VND", fields["vnp_CurrCode"]);
        Assert.Equal("15000000", fields["vnp_Amount"]); Assert.NotNull(fields["vnp_CreateDate"]);
        Assert.Equal("127.0.0.1", fields["vnp_IpAddr"]); Assert.Null(fields["vnp_CreateTime"]); Assert.Null(fields["vnp_CurrType"]);
    }

    [Fact]
    public async Task Zalo_v2_uses_key1_form_mac_milliseconds_and_provider_order_url()
    {
        const string reference = "261024_test_order";
        var handler = new CaptureHandler((request, body) => {
            Assert.Equal("https://sb-openapi.zalopay.vn/v2/create", request.RequestUri!.AbsoluteUri);
            Assert.Equal("application/x-www-form-urlencoded", request.Content!.Headers.ContentType!.MediaType);
            var r = System.Web.HttpUtility.ParseQueryString(body);
            Assert.Equal(reference, r["app_trans_id"]); Assert.Equal("1234", r["app_id"]);
            Assert.True(long.Parse(r["app_time"]!) > 1000000000000L);
            var canonical = $"1234|{reference}|BadmintonBooking|150000|{r["app_time"]}|{r["embed_data"]}|[]";
            var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("key1"), Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
            Assert.Equal(expected, r["mac"]); Assert.Equal("https://api.example.test/callback", r["callback_url"]);
            return "{\"return_code\":1,\"order_url\":\"https://sbgateway.zalopay.vn/checkout\"}";
        });
        using var http = new HttpClient(handler);
        var settings = new ZalopaySettings { AppId = "1234", ApiKey = "key1", CallbackKey = "key2", CallbackUrl = "https://api.example.test/callback" };
        var result = await new ZalopayGateway(settings, new HttpZalopayClient(http, settings)).InitAsync(reference, 150000, "https://web.example.test/return");
        Assert.True(result.Ok); Assert.Equal(reference, result.GatewayRef); Assert.Equal("https://sbgateway.zalopay.vn/checkout", result.GatewayUrl);
    }

    [Fact]
    public async Task Optional_nested_callback_metadata_does_not_invalidate_signed_scalar_fields()
    {
        var momo = PaymentCallbackFixtures.MoMo("key", "ORDER");
        momo = momo with { Body = momo.Body[..^1] + ",\"promotionInfo\":null}" };
        var gateway = new MoMoGateway(new MoMoSettings { AppId = "test", AccessKey = "audit-access", SecretKey = "key" }, new UnusedMomo());
        Assert.True((await gateway.VerifyWebhookAsync(momo)).Verified);
        momo = momo with { Body = momo.Body[..^1] + ",\"promotionInfo\":[]}" };
        Assert.False((await gateway.VerifyWebhookAsync(momo)).Verified); // duplicated optional fields remain forbidden
    }
    private sealed class UnusedMomo : IMomoApiClient
    {
        public Task<MomoPayInit> CreatePayUrlAsync(MomoPayRequest request, CancellationToken ct) => throw new InvalidOperationException("No network.");
    }

    private sealed class CaptureHandler(Func<HttpRequestMessage, string, string> respond) : HttpMessageHandler
    {
        public int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return new(HttpStatusCode.OK) { Content = new StringContent(respond(request, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)), Encoding.UTF8, "application/json") };
        }
    }
}
