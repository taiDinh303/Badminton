using Core.Abstractions;
using Core.Enums;

namespace Core.Strategies.Payment
{
    /// <summary>
    /// Chuyển khoản ngân hàng – khách chuyển vào tài khoản sân,
    /// xác nhận bằng mã giao dịch do khách nhập.
    /// Trạng thái ở chế độ chờ (Pending) đến khi quản lý xác nhận.
    /// </summary>
    public class BankTransferPaymentProcessor : IPaymentProcessor
    {
        public PaymentMethod Method => PaymentMethod.BankTransfer;

        public string DisplayName => "Chuyển khoản";

        public Task<PaymentResult> ProcessAsync(decimal amount, string bookingCode)
        {
            // Chuyển khoản: ghi nhận đơn, trạng thái chờ xác nhận
            var txnCode = $"BKTR-{bookingCode}-{DateTime.UtcNow:yyyyMMddHHmm}";
            return Task.FromResult<PaymentResult>(
                new(true, txnCode, "Đã ghi nhận chuyển khoản – chờ quản lý xác nhận."));
        }

        public Task<bool> RefundAsync(string transactionCode, decimal amount)
        {
            // Hoàn khoản: chuyển khoản ngược về tài khoản khách
            return Task.FromResult(true);
        }

        public Task<PaymentStatusInfo> CheckStatusAsync(string transactionCode)
        {
            // Chờ đối soát (mock) – thực tế sẽ query ngân hàng / cổng
            return Task.FromResult(PaymentStatusInfo.Pending(transactionCode, 0));
        }
    }
}
