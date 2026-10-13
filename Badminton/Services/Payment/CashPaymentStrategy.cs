using M.Core.Domain;
using M.Core.Enums;

namespace Services
{
    /// <summary>Cash paid at the counter — succeeds immediately, no external call.</summary>
    public sealed class CashPaymentStrategy : BasePaymentStrategy
    {
        public override PaymentMethod Method => PaymentMethod.Cash;

        protected override Task<PaymentResult> DoPayAsync(
            PaymentContext context, string txnCode, CancellationToken ct)
        {
            // Cash is a synchronous, in-person capture. Real-world you would
            // record the teller; here we simply mark it paid.
            return Task.FromResult(PaymentResult.Ok(txnCode));
        }
    }
}
