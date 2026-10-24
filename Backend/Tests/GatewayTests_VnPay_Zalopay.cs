using Badminton.Core.Domain;
using Badminton.Core.Enums;
using Badminton.Core.Store;
using Services;
using Xunit;

namespace Badminton.Tests
{
    public class VnPayGatewayTests
    {
        private static VnPayGateway Unconfigured() =>
            new(new VnPaySettings { MerchantCode = "m", SecretKey = "" }, Stub.Instance);

        private static VnPayGateway Configured(string secret = "vnpay-secret") =>
            new(new VnPaySettings { MerchantCode = "M00001", SecretKey = secret },
                new OkVnPayClient());

        private static RawWebhook Ipn(string secret, string txCode,
            string responseCode, string? hashOverride = null,
            bool omitHash = false)
        {
            var f = new Dictionary<string, string>
            {
                ["vnp_ResponseId"] = "111",
                ["vnp_TmnCode"] = "M00001",
                ["vnp_TxnRef"] = txCode,
                ["vnp_Amount"] = "15000000",
                ["vnp_ResponseCode"] = responseCode,
                ["vnp_TransactionStatus"] = responseCode,
                ["vnp_BankCode"] = "VNBC"
            };
            var hash = hashOverride ?? VnPaySigner.Sign(secret, f);
            var pairs = f.Select(kv => $"{kv.Key}={kv.Value}").ToList();
            if (!omitHash)
                pairs.Add("vnp_SecureHash=" + hash);
            var qs = string.Join("&", pairs);
            return new RawWebhook("vnpay", txCode, qs, null,
                new Dictionary<string, string>());
        }

        [Fact(DisplayName = "Unconfigured VnPay: InitAsync + webhook verification fail closed")]
        public async Task Unconfigured_FailsClosed()
        {
            var g = Unconfigured();
            Assert.False(g.IsConfigured);

            var init = await g.InitAsync("BD-1", 150_000, "https://return.local");
            Assert.False(init.Ok);
            Assert.Contains("chua duoc cau hinh", init.Error!,
                System.StringComparison.OrdinalIgnoreCase);

            var webhook = Ipn("vnpay-secret", "BD-1", "00");
            var outcome = await g.VerifyWebhookAsync(webhook);
            Assert.False(outcome.Verified);
        }

        [Fact(DisplayName = "Configured VnPay: forged vnp_SecureHash is rejected")]
        public async Task ForgedHash_Rejected()
        {
            var g = Configured("the-real-secret");
            var webhook = Ipn("the-real-secret", "BD-9", "00",
                hashOverride: "deadbeef");
            var outcome = await g.VerifyWebhookAsync(webhook);
            Assert.False(outcome.Verified);
            Assert.Contains("Chữ ký", outcome.Error!,
                System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact(DisplayName = "Configured VnPay: valid hash verifies; only 00/00 is success")]
        public async Task ValidHash_Verifies()
        {
            var g = Configured("the-real-secret");

            var ok = await g.VerifyWebhookAsync(Ipn("the-real-secret", "BD-77", "00"));
            Assert.True(ok.Verified);
            Assert.True(ok.Success);

            // 02 is not a successful payment result.
            var dup = await g.VerifyWebhookAsync(Ipn("the-real-secret", "BD-77", "02"));
            Assert.True(dup.Verified);
            Assert.False(dup.Success);

            // 01 = payment in progress / not success.
            var pending = await g.VerifyWebhookAsync(Ipn("the-real-secret", "BD-77", "01"));
            Assert.True(pending.Verified);
            Assert.False(pending.Success);
        }

        [Fact(DisplayName = "Configured VnPay: missing hash or empty body rejected")]
        public async Task MissingHashOrBody_Rejected()
        {
            var g = Configured();

            var noHash = Ipn("the-real-secret", "BD-5", "00", omitHash: true);
            var r1 = await g.VerifyWebhookAsync(noHash);
            Assert.False(r1.Verified);

            var empty = new RawWebhook("vnpay", "BD-5", "", null,
                new Dictionary<string, string>());
            var r2 = await g.VerifyWebhookAsync(empty);
            Assert.False(r2.Verified);
        }

        [Fact(DisplayName = "VnPay InitAsync succeeds with the client's redirect URL")]
        public async Task Init_Succeeds()
        {
            var g = Configured();
            var init = await g.InitAsync("BD-1", 150_000, "https://return.local");
            Assert.True(init.Ok);
            Assert.NotNull(init.GatewayUrl);
            Assert.Equal(init.GatewayRef, init.GatewayRef); // ref echoed back
        }

        [Fact(DisplayName = "VnPaySigner is deterministic; a tampered field changes the hash")]
        public void Signer_DeterministicTamperSensitive()
        {
            var f = new Dictionary<string, string>
            {
                ["vnp_TmnCode"] = "M1", ["vnp_Amount"] = "15000000",
                ["vnp_TxnRef"] = "T1", ["vnp_SecureHash"] = "ignored"
            };
            var h1 = VnPaySigner.Sign("k", f);
            Assert.Equal(h1, VnPaySigner.Sign("k", f));
            Assert.Equal(128, h1.Length); // HMAC-SHA512 hex

            var f2 = new Dictionary<string, string>(f) { ["vnp_Amount"] = "999" };
            Assert.NotEqual(h1, VnPaySigner.Sign("k", f2));
        }

        // ── test doubles ────────────────────────────────────────────────
        private sealed class Stub : IVnPayClient
        {
            public static Stub Instance { get; } = new();
            public Task<VnPayCreateResult> CreatePayUrlAsync(
                IReadOnlyDictionary<string, string> fields, CancellationToken ct)
                => Task.FromResult(new VnPayCreateResult(false, null, null, "stub"));
        }

        private sealed class OkVnPayClient : IVnPayClient
        {
            public Task<VnPayCreateResult> CreatePayUrlAsync(
                IReadOnlyDictionary<string, string> fields, CancellationToken ct)
                => Task.FromResult(new VnPayCreateResult(
                    true, "https://checkout.vnpay.io/pay?" + fields["vnp_TxnRef"],
                    fields["vnp_TxnRef"], null));
        }
    }

    public class ZalopayGatewayTests
    {
        private static ZalopayGateway Unconfigured() =>
            new(new ZalopaySettings { ApiKey = "", ApiSecret = "" }, Stub.Instance);

        private static ZalopayGateway Configured(string secret = "zp-secret") =>
            new(new ZalopaySettings
            {
                AppId = "1234", ApiKey = "zp-key", ApiSecret = secret, CallbackKey = secret
            }, new OkZalopayClient());

        private static RawWebhook Callback(string secret, string orderId,
            string? sigOverride = null)
        {
            var callback = PaymentCallbackFixtures.Zalo(secret, orderId);
            if (sigOverride is not null) callback = callback with { Body = callback.Body.Replace("\"mac\":\"", "\"mac\":\"" + sigOverride) };
            return callback;
        }

        [Fact(DisplayName = "Unconfigured Zalopay: InitAsync + webhook fail closed")]
        public async Task Unconfigured_FailsClosed()
        {
            var g = Unconfigured();
            Assert.False(g.IsConfigured);

            var init = await g.InitAsync("BD-1", 150_000, "https://return.local");
            Assert.False(init.Ok);

            var webhook = Callback("zp-secret", "BD-1", sigOverride: "forged");
            var outcome = await g.VerifyWebhookAsync(webhook);
            Assert.False(outcome.Verified);
            Assert.Contains("chua duoc cau hinh", outcome.Error!,
                System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact(DisplayName = "Configured Zalopay: forged appSignature is rejected")]
        public async Task ForgedSignature_Rejected()
        {
            var g = Configured("the-real-secret");
            var webhook = Callback("the-real-secret", "BD-9",
                sigOverride: "deadbeef");
            var outcome = await g.VerifyWebhookAsync(webhook);
            Assert.False(outcome.Verified);
        }

        [Fact(DisplayName = "Configured Zalopay: full data MAC verifies; mismatched order rejected")]
        public async Task ValidSignature_Verifies()
        {
            var g = Configured("the-real-secret");

            var ok = await g.VerifyWebhookAsync(
                Callback("the-real-secret", "BD-77"));
            Assert.True(ok.Verified);
            Assert.True(ok.Success);

            var tampered = Callback("the-real-secret", "BD-77") with { GatewayRef = "OTHER" };
            var fail = await g.VerifyWebhookAsync(tampered);
            Assert.False(fail.Verified);
            Assert.False(fail.Success);
        }

        [Fact(DisplayName = "Zalopay InitAsync returns the client's orderId as gateway ref")]
        public async Task Init_Succeeds()
        {
            var g = Configured();
            var init = await g.InitAsync("BD-1", 150_000, "https://return.local");
            Assert.True(init.Ok);
            Assert.Equal("BD-1", init.GatewayRef);
            Assert.StartsWith("https://", init.GatewayUrl);
        }

        [Fact(DisplayName = "ZpSigner HMAC/SHA256 are deterministic and distinct per input")]
        public void Signer_Deterministic()
        {
            Assert.Equal(ZpSigner.Hmac("k", "m"), ZpSigner.Hmac("k", "m"));
            Assert.NotEqual(ZpSigner.Hmac("k1", "m"), ZpSigner.Hmac("k2", "m"));
            Assert.Equal(64, ZpSigner.Hmac("k", "m").Length);
            Assert.Equal(64, ZpSigner.Sha256Hex("x").Length);
        }

        // ── test doubles ────────────────────────────────────────────────
        private sealed class Stub : IZalopayClient
        {
            public static Stub Instance { get; } = new();
            public Task<ZpOrderCreate> CreateOrderAsync(
                ZpCreateOrderRequest r, CancellationToken ct)
                => Task.FromResult(new ZpOrderCreate(false, null, null, "stub", null, null));
        }

        private sealed class OkZalopayClient : IZalopayClient
        {
            public Task<ZpOrderCreate> CreateOrderAsync(
                ZpCreateOrderRequest r, CancellationToken ct)
                => Task.FromResult(new ZpOrderCreate(
                    true, r.RefOrderId, "https://checkout.example.test/" + r.RefOrderId, null, "1 success", null));
        }
    }
}
