using M.Core.Domain;
using M.Core.Enums;

namespace Services
{
    /// <summary>
    /// ABSTRACTION (trừu tượng): a base payment strategy that centralises the
    /// validation + transaction-code generation every method needs. Concrete
    /// strategies (Cash / BankTransfer / QRCode) only override the parts that
    /// differ. This is the TEMPLATE + POLYMORPHISM (đa hình) pair: one shared
    /// flow, many replaceable steps.
    /// </summary>
    public abstract class BasePaymentStrategy : IPaymentStrategy
    {
        public abstract PaymentMethod Method { get; }

        /// <summary>Shared validation before the method-specific step.</summary>
        protected void Validate(PaymentContext context)
        {
            if (context.Amount <= 0)
                throw new M.Core.Base.BadRequestException(
                    "Payment amount must be positive.");
        }

        /// <summary>Deterministic transaction code shared by all methods.</summary>
        protected static string GenerateTransactionCode(int bookingId, PaymentMethod method) =>
            "TXN-" + bookingId.ToString().PadLeft(4, '0') + "-" +
            method.ToString() + "-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        /// <summary>Method-specific payment logic. Encapsulated here per strategy.</summary>
        protected abstract Task<PaymentResult> DoPayAsync(PaymentContext context, string txnCode, CancellationToken ct);

        protected virtual Task<PaymentResult> DoRefundAsync(PaymentContext context, CancellationToken ct) =>
            Task.FromResult(PaymentResult.Ok(GenerateTransactionCode(context.BookingId, Method)));

        public Task<PaymentResult> PayAsync(PaymentContext context, CancellationToken ct = default)
        {
            Validate(context);
            var code = context.TransactionReference ?? GenerateTransactionCode(context.BookingId, Method);
            return DoPayAsync(context, code, ct);
        }

        public Task<PaymentResult> RefundAsync(PaymentContext context, CancellationToken ct = default) =>
            DoRefundAsync(context, ct);
    }
}
