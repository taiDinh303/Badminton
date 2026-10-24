using Badminton.Core.Domain;
using Badminton.Core.Enums;
using Badminton.Core.Store;
using Microsoft.Extensions.Options;
using Services;
using Xunit;

namespace Badminton.Tests
{
    public class MoMoGatewayTests
    {
        private static MoMoGateway UnconfiguredGateway() =>
            new(new MoMoSettings { AppId = "test", SecretKey = "" },
                StubMomoClient.Instance);

        private static MoMoGateway ConfiguredGateway(string secret) =>
            new(new MoMoSettings { AppId = "test", SecretKey = secret, AccessKey = "audit-access", IpayUrl = "https://api.example.test/api/v1/payments/callback/momo" },
                StubMomoClient.Instance);

        private static readonly MomoPayInit OkInit = new(
            true, "https://test.tapmomo.vn/wallet/checkout?orderId=12345",
            "BD-1", null, null);

        [Fact(DisplayName = "Unconfigured MoMo gateway: InitAsync fails, webhook verification fails closed")]
        public async Task UnconfiguredGateway_FailsClosed()
        {
            var g = UnconfiguredGateway();
            Assert.False(g.IsConfigured);

            var init = await g.InitAsync("BD-1", 100_000, "https://return.local");
            Assert.False(init.Ok);
            Assert.NotEmpty(init.Error!);

            var webhook = new RawWebhook("momo", "BD-1",
                """{"orderId":"BD-1","responseStatus":"0","responseKey":"forged"}""",
                null, new Dictionary<string, string>());
            var outcome = await g.VerifyWebhookAsync(webhook);
            Assert.False(outcome.Verified);
            Assert.Contains("MoMo chua duoc cau hinh", outcome.Error,
                System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact(DisplayName = "Configured MoMo gateway: a forged responseKey is rejected")]
        public async Task ForgedResponseKey_IsRejected()
        {
            const string secret = "shared-secret-1234567890";
            var g = ConfiguredGateway(secret);

            // A responseKey NOT derived from the real shared secret must fail.
            var webhook = new RawWebhook("momo", "BD-9",
                """{"orderId":"BD-9","responseStatus":"0","responseKey":"deadbeef"}""",
                null, new Dictionary<string, string>());
            var outcome = await g.VerifyWebhookAsync(webhook);
            Assert.False(outcome.Verified);
        }

        [Fact(DisplayName = "Configured MoMo gateway: signed full IPN verifies, signed failure is not success")]
        public async Task FullIpnSignature_Verifies()
        {
            const string secret = "shared-secret-1234567890";
            var g = ConfiguredGateway(secret);

            var success = await g.VerifyWebhookAsync(PaymentCallbackFixtures.MoMo(secret, "BD-77"));
            Assert.True(success.Verified);
            Assert.True(success.Success);
            Assert.Equal(150000, success.Amount);
            var failed = await g.VerifyWebhookAsync(PaymentCallbackFixtures.MoMo(secret, "BD-77", result: 400));
            Assert.True(failed.Verified);
            Assert.False(failed.Success);
        }

        [Fact(DisplayName = "Configured MoMo gateway: InitAsync succeeds when the client returns a pay URL")]
        public async Task ConfiguredInit_Succeeds()
        {
            const string secret = "shared-secret-1234567890";
            var g = new MoMoGateway(
                new MoMoSettings { AppId = "test", SecretKey = secret, AccessKey = "audit-access", IpayUrl = "https://api.example.test/api/v1/payments/callback/momo" },
                new OkMomoClient(OkInit));

            var init = await g.InitAsync("BD-1", 150_000, "https://return.local");
            Assert.True(init.Ok);
            Assert.NotNull(init.GatewayUrl);
        }

        [Fact(DisplayName = "MoMoSigner primitives are deterministic and distinct per input")]
        public void Signer_IsDeterministicAndDistinct()
        {
            Assert.Equal(MoMoSigner.ResponseKey("A", "k"), MoMoSigner.ResponseKey("A", "k"));
            Assert.NotEqual(MoMoSigner.ResponseKey("A", "k1"), MoMoSigner.ResponseKey("A", "k2"));
            Assert.NotEqual(MoMoSigner.ResponseKey("A", "k"), MoMoSigner.ResponseKey("B", "k"));

            // request id / access key derivation is stable.
            Assert.Equal(MoMoSigner.RequestId(1735689600000), MoMoSigner.RequestId(1735689600000));
            Assert.NotEqual(MoMoSigner.AccessKey("r", "k1"), MoMoSigner.AccessKey("r", "k2"));

            // The request-sign scheme is a 128-char lower-hex SHA-512 digest.
            var sign = MoMoSigner.ComputeSign(new Dictionary<string, string>
            {
                ["b_partnerCode"] = "partner", ["b_partnerRef"] = "1",
                ["b_amount"] = "100000", ["b_timestamp"] = "123"
            });
            Assert.Equal(128, sign.Length);
            Assert.Equal(sign, sign.ToLowerInvariant());
        }

        // ── test doubles ────────────────────────────────────────────────

        private sealed class StubMomoClient : IMomoApiClient
        {
            public static StubMomoClient Instance { get; } = new();
            public Task<MomoPayInit> CreatePayUrlAsync(MomoPayRequest r, CancellationToken ct)
                => Task.FromResult(new MomoPayInit(false, null, null, "stub: not configured", null));
        }

        private sealed class OkMomoClient : IMomoApiClient
        {
            private readonly MomoPayInit _r;
            public OkMomoClient(MomoPayInit r) => _r = r;
            public Task<MomoPayInit> CreatePayUrlAsync(MomoPayRequest r, CancellationToken ct)
                => Task.FromResult(_r);
        }
    }
}
