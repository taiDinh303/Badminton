using M.Core.Domain;
using M.Core.Enums;

namespace Services
{
    /// <summary>Bank transfer — assumes the reference already exists; verify on hold.</summary>
    public sealed class BankTransferPaymentStrategy : BasePaymentStrategy
    {
        public override PaymentMethod Method => PaymentMethod.BankTransfer;

        protected override Task<PaymentResult> DoPayAsync(
            PaymentContext context, string txnCode, CancellationToken ct)
        {
            // A real integration would call the bank gateway + reconcile the
            // inbound transfer. For the demo we accept the supplied reference
            // and mark the payment paid.
            if (string.IsNullOrWhiteSpace(context.TransactionReference))
            {
                return Task.FromResult(PaymentResult.Fail(
                    "Bank transfer requires a transaction reference."));
            }
            return Task.FromResult(PaymentResult.Ok(txnCode));
        }
    }
}
