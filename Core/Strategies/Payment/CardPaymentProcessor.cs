using Core.Abstractions;
using Core.Enums;

namespace Core.Strategies.Payment
{
    /// <summary>
    /// Thanh toán qua thẻ (Visa/MasterCard) – tích hợp cổng thanh toán.
    /// Ở chế độ mock: luôn giả lập thành công để test luồng.
    /// </summary>
    public class CardPaymentProcessor : IPaymentProcessor
    {
        public PaymentMethod Method => PaymentMethod.Card;

        public string DisplayName => "Thẻ (Visa/MC)";

        public Task<PaymentResult> ProcessAsync(decimal amount, string bookingCode)
        {
            var txnCode = $"CARD-{bookingCode}-{DateTime.UtcNow:yyyyMMddHHmm}";
            return Task.FromResult<PaymentResult>(
                new(true, txnCode, "Giao dịch thẻ thành công."));
        }

        public Task<bool> RefundAsync(string transactionCode, decimal amount)
        {
            // Hoàn tiền qua thẻ: gọi API cổng hoàn tiền
            return Task.FromResult(true);
        }

        public Task<PaymentStatusInfo> CheckStatusAsync(string transactionCode)
        {
            return Task.FromResult(PaymentStatusInfo.Success(
                transactionCode, 0, DateTimeOffset.UtcNow));
        }
    }
}
