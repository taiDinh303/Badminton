using Badminton.Core.Domain;
using Badminton.Core.Enums;

namespace Services
{
    public sealed class MockOnlineGateway : IPaymentGateway
    {
        public bool IsSimulation => true;
        public PaymentMethod Method { get; }

        public MockOnlineGateway(PaymentMethod method) => Method = method;

        public Task<PaymentInitResult> InitAsync(string bookingId, decimal amount,
            string returnUrl, CancellationToken ct = default)
        {
            var refCode = bookingId;
            // In production this URL is signed by the provider's SDK.
            return Task.FromResult(PaymentInitResult.Success(refCode,
                $"https://mock-gateway.local/{Method.ToString().ToLowerInvariant()}/checkout?ref={refCode}"));
        }

        public Task<WebhookOutcome> VerifyWebhookAsync(RawWebhook raw, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(raw.GatewayRef))
                return Task.FromResult(new WebhookOutcome(
                    Verified: false, Success: false, null, "webhook thieu gateway_ref."));

            bool success = true;
            if (raw.Body is { } body && !string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    var map = System.Text.Json.JsonSerializer
                        .Deserialize<Dictionary<string, string>>(
                            body,
                            new System.Text.Json.JsonSerializerOptions
                            { PropertyNameCaseInsensitive = true });
                    if (map is not null && map.TryGetValue("status", out var s))
                        success = s?.Equals("success", System.StringComparison.OrdinalIgnoreCase) == true;
                }
                catch
                {
                    // Malformed body we can't parse → don't trust it.
                    success = false;
                }
            }

            return Task.FromResult(new WebhookOutcome(
                Verified: true, Success: success, raw.GatewayRef, null));
        }

        public Task<RefundResult> RefundAsync(string bookingId, decimal amount,
            string reason, CancellationToken ct = default) =>
            Task.FromResult(new RefundResult(true, amount, null));
    }

    public sealed class OnSiteGateway : IPaymentGateway
    {
        public PaymentMethod Method { get; }

        public OnSiteGateway(PaymentMethod method) => Method = method;

        public Task<PaymentInitResult> InitAsync(string bookingId, decimal amount,
            string returnUrl, CancellationToken ct = default) =>
            Task.FromResult(new PaymentInitResult(false, null, null,
                "Thanh toan tai quay do manager xac nhan, khong co URL gateway."));

        public Task<WebhookOutcome> VerifyWebhookAsync(RawWebhook raw, CancellationToken ct = default) =>
            Task.FromResult(new WebhookOutcome(false, false, raw.GatewayRef,
                "On-site payments have no gateway webhook."));

        public Task<RefundResult> RefundAsync(string bookingId, decimal amount,
            string reason, CancellationToken ct = default) =>
            Task.FromResult(new RefundResult(true, amount, null));
    }

    public sealed class PaymentGatewayFactory : IPaymentGatewayFactory
    {
        private readonly IReadOnlyDictionary<PaymentMethod, IPaymentGateway> _map;

        public PaymentGatewayFactory(IEnumerable<IPaymentGateway> gateways)
        {
            _map = gateways.ToDictionary(g => g.Method);
        }

        public IPaymentGateway GetGateway(PaymentMethod method) =>
            _map.TryGetValue(method, out var g)
                ? g
                : throw new Badminton.Core.Base.BadRequestException($"Unsupported payment method '{method}'.");

        public IPaymentGateway GetGatewayByProvider(string provider)
        {
            if (Enum.TryParse<PaymentMethod>(provider, ignoreCase: true, out var m))
            {
                if (m is PaymentMethod.Cash or PaymentMethod.QR)
                    throw new Badminton.Core.Base.BadRequestException(
                        "On-site providers do not emit webhooks.");
                return GetGateway(m);
            }
            throw new Badminton.Core.Base.BadRequestException(
                $"Unknown payment provider '{provider}'.");
        }
    }
}
