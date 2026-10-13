using M.Core.Enums;

namespace M.Core.Domain
{
    public interface IPaymentGateway
    {
        PaymentMethod Method { get; }

        Task<PaymentInitResult> InitAsync(
            string bookingId, decimal amount, string returnUrl, CancellationToken ct = default);

        Task<WebhookOutcome> VerifyWebhookAsync(RawWebhook raw, CancellationToken ct = default);

        Task<RefundResult> RefundAsync(string bookingId, decimal amount, string reason,
            CancellationToken ct = default);
    }

    public sealed record RawWebhook(string Provider, string GatewayRef, string Body,
        string Signature, IReadOnlyDictionary<string, string> Headers);

    public sealed record PaymentInitResult(bool Ok, string? GatewayUrl, string? GatewayRef,
        string? Error)
    {
        public static PaymentInitResult Success(string gatewayRef, string gatewayUrl) =>
            new(true, gatewayUrl, gatewayRef, null);
        public static PaymentInitResult Fail(string error) =>
            new(false, null, null, error);
    }

    public sealed record WebhookOutcome(bool Verified, bool Success,
        string? GatewayRef, string? Error);

    public sealed record RefundResult(bool Ok, decimal Amount, string? Error)
    {
        public static RefundResult Fail(string error) => new(false, 0m, error);
    }

    public interface IPaymentGatewayFactory
    {
        IPaymentGateway GetGateway(PaymentMethod method);
    }
}
