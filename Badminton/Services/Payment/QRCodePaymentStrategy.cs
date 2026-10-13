using M.Core.Domain;
using M.Core.Enums;

namespace Services
{
    /// <summary>QRCode (wallet) payment — capture the wallet transaction id.</summary>
    public sealed class QRCodePaymentStrategy : BasePaymentStrategy
    {
        public override PaymentMethod Method => PaymentMethod.QRCode;

        protected override Task<PaymentResult> DoPayAsync(
            PaymentContext context, string txnCode, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(context.TransactionReference))
            {
                return Task.FromResult(PaymentResult.Fail(
                    "QRCode payment requires a wallet transaction id."));
            }
            return Task.FromResult(PaymentResult.Ok(txnCode));
        }

        protected override Task<PaymentResult> DoRefundAsync(
            PaymentContext context, CancellationToken ct) =>
            Task.FromResult(PaymentResult.Ok(
                GenerateTransactionCode(context.BookingId, Method)));
    }
}
