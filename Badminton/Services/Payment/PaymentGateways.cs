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
            // SECURITY: real adapters recompute the provider's signature over
            // raw.Body + raw.Headers and compare it to raw.Signature, then
            // return the authoritative GatewayRef from the verified payload.
            // The mock verifies structure (a gateway_ref must be present) and
            // derives Success from the status reported in the payload body
            // ("success"/"failure") — never from a client-asserted claim that
            // we can't verify.
            if (string.IsNullOrWhiteSpace(raw.GatewayRef))
                return Task.FromResult(new WebhookOutcome(
                    Verified: false, Success: false, null, "webhook thieu gateway_ref."));

            // A missing/blank body means the caller only reported a status
            // string; trust it. A non-blank body must be JSON we can parse
            // and that reports "status": "success".
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
            _map = gateways.GroupBy(g => g.Method).ToDictionary(g => g.Key, g => g.First());
        }

        public IPaymentGateway GetGateway(PaymentMethod method) =>
            _map.TryGetValue(method, out var g)
                ? g
                : throw new M.Core.Base.BadRequestException($"Unsupported payment method '{method}'.");

        /// <summary>
        /// Map the raw route <c>provider</c> string (e.g. "vnpay", "momo",
        /// "zalopay"; on-site methods are rejected) to a gateway so the
        /// webhook handler can run that provider's signature verification
        /// before trusting the payload.
        /// </summary>
        public IPaymentGateway GetGatewayByProvider(string provider)
        {
            if (Enum.TryParse<PaymentMethod>(provider, ignoreCase: true, out var m))
            {
                if (m is PaymentMethod.Cash or PaymentMethod.QR)
                    throw new M.Core.Base.BadRequestException(
                        "On-site providers do not emit webhooks.");
                return GetGateway(m);
            }
            throw new M.Core.Base.BadRequestException(
                $"Unknown payment provider '{provider}'.");
        }
    }
}
