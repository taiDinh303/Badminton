using M.Core.Enums;

namespace M.Core.Domain
{
    /// <summary>
    /// ABSTRACTION (trừu tượng): the contract every payment method obeys.
    /// No concrete code here. Implementations (Cash / BankTransfer / QRCode)
    /// live in the SERVICES layer so the API only ever talks to this
    /// interface. That is the whole point of abstraction.
    /// </summary>
    public interface IPaymentStrategy
    {
        /// <summary>Which method this strategy handles.</summary>
        PaymentMethod Method { get; }

        /// <summary>Execute the payment. Implementations decide their own rules.</summary>
        Task<PaymentResult> PayAsync(PaymentContext context, CancellationToken ct = default);

        /// <summary>Refund, where the method supports it.</summary>
        Task<PaymentResult> RefundAsync(PaymentContext context, CancellationToken ct = default);
    }

    /// <summary>Everything a strategy needs to charge a booking.</summary>
    public sealed record PaymentContext
    {
        public required int BookingId { get; init; }
        public required string BookingCode { get; init; }
        public required decimal Amount { get; init; }
        public required int UserId { get; init; }
        public string? TransactionReference { get; init; }
    }

    /// <summary>Outcome a strategy returns; the service maps it to entities.</summary>
    public sealed record PaymentResult(
        bool Succeeded,
        PaymentStatus Status,
        string? TransactionCode,
        string? Error)
    {
        public static PaymentResult Ok(string? code = null) =>
            new(true, PaymentStatus.Paid, code, null);

        public static PaymentResult Fail(string error) =>
            new(false, PaymentStatus.Failed, null, error);
    }

    /// <summary>
    /// POLYMORPHISM selection point (đa hình): given a method, resolve the
    /// right strategy. The factory hides "which implementation" so callers
    /// never switch on the enum themselves.
    /// </summary>
    public interface IPaymentStrategyFactory
    {
        IPaymentStrategy GetStrategy(PaymentMethod method);
    }
}
