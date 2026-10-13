using Core.Abstractions;
using Core.Enums;

namespace Core.Strategies.Payment
{
    /// <summary>
    /// Thanh toán tiền mặt – người chơi trả trực tiếp tại sân.
    /// Transaction code được sinh bằng UUID để ghi nhận tại quầy.
    /// </summary>
    public class CashPaymentProcessor : IPaymentProcessor
    {
        public PaymentMethod Method => PaymentMethod.Cash;

        public string DisplayName => "Tiền mặt";

        public Task<PaymentResult> ProcessAsync(decimal amount, string bookingCode)
        {
            // Tiền mặt: luôn thành công ngay (quản lý xác nhận bằng mắt)
            var txnCode = $"CASH-{bookingCode}-{DateTime.UtcNow:yyyyMMddHHmm}";
            return Task.FromResult<PaymentResult>(
                new(true, txnCode, "Thanh toán tiền mặt ghi nhận thành công."));
        }

        public Task<bool> RefundAsync(string transactionCode, decimal amount)
        {
            // Hoàn tiền mặt: thủ công tại quầy
            return Task.FromResult(true);
        }

        public Task<PaymentStatusInfo> CheckStatusAsync(string transactionCode)
        {
            return Task.FromResult(PaymentStatusInfo.Success(
                transactionCode, 0, DateTimeOffset.UtcNow));
        }
    }
}
