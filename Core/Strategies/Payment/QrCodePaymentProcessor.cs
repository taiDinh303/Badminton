using Core.Abstractions;
using Core.Enums;

namespace Core.Strategies.Payment
{
    /// <summary>
    /// Thanh toán qua mã QR (Momo, VietQR, ViettelPay…).
    /// Khách quét mã, cổng xác nhận gần như tức thì.
    /// </summary>
    public class QrCodePaymentProcessor : IPaymentProcessor
    {
        public PaymentMethod Method => PaymentMethod.QrCode;

        public string DisplayName => "QR (Momo/VietQR)";

        public Task<PaymentResult> ProcessAsync(decimal amount, string bookingCode)
        {
            var txnCode = $"QR-{bookingCode}-{DateTime.UtcNow:yyyyMMddHHmm}";
            return Task.FromResult<PaymentResult>(
                new(true, txnCode, "Thanh toán QR đã hoàn tất."));
        }

        public Task<bool> RefundAsync(string transactionCode, decimal amount)
        {
            // Hoàn tiền QR: xử lý qua cổng
            return Task.FromResult(true);
        }

        public Task<PaymentStatusInfo> CheckStatusAsync(string transactionCode)
        {
            return Task.FromResult(PaymentStatusInfo.Success(
                transactionCode, 0, DateTimeOffset.UtcNow));
        }
    }
}
