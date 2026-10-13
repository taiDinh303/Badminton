using M.Core.Domain;
using M.Core.Enums;

namespace Services
{
    public sealed class MockOnlineGateway : IPaymentGateway
    {
        public PaymentMethod Method { get; }

        public MockOnlineGateway(PaymentMethod method) => Method = method;

        public Task<PaymentInitResult> InitAsync(string bookingId, decimal amount,
            string returnUrl, CancellationToken ct = default)
        {
            var refCode = $"MOCK-{Method}-{bookingId}-{Guid.NewGuid():N}".ToUpperInvariant()[..24];
            // In production this URL is signed by the provider's SDK.
            return Task.FromResult(PaymentInitResult.Success(refCode,
                $"https://mock-gateway.local/{Method.ToString().ToLowerInvariant()}/checkout?ref={refCode}"));
        }

        public Task<WebhookOutcome> VerifyWebhookAsync(RawWebhook raw, CancellationToken ct = default)
        {
            // Real adapters verify the signature here. The mock trusts a
            // well-formed payload and reports it verified.
            return Task.FromResult(new WebhookOutcome(
                Verified: true, Success: true, raw.GatewayRef, null));
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
                "Thanh toÃ¡n táº¡i quáº§y do manager xÃ¡c nháº­n, khÃ´ng cÃ³ URL gateway."));

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
            _map = gateways.GroupBy(g => g.Method).ToDictionary(g => g.Key, g => g.First());
        }

        public IPaymentGateway GetGateway(PaymentMethod method) =>
            _map.TryGetValue(method, out var g)
                ? g
                : throw new M.Core.Base.BadRequestException($"Unsupported payment method '{method}'.");
    }
}
