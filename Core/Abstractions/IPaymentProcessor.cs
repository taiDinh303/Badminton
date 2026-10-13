using Core.Enums;

namespace Core.Abstractions
{
    /// <summary>
    /// Trừu tượng cho bộ xử lý thanh toán.
    /// Mỗi phương thức thanh toán (tiền mặt, chuyển khoản, thẻ, QR)
    /// là một lớp riêng kế thừa AbstractPaymentProcessor.
    /// </summary>
    public interface IPaymentProcessor
    {
        /// <summary>Phương thức thanh toán.</summary>
        PaymentMethod Method { get; }

        /// <summary>Tên hiển thị cho UI.</summary>
        string DisplayName { get; }

        /// <summary>
        /// Xử lý thanh toán cho một giao dịch.
        /// Trả về mã giao dịch (transaction code) sau khi thành công.
        /// </summary>
        Task<PaymentResult> ProcessAsync(decimal amount, string bookingCode);

        /// <summary>
        /// Hoàn tiền (nếu phương thức hỗ trợ).
        /// </summary>
        Task<bool> RefundAsync(string transactionCode, decimal amount);

        /// <summary>
        /// Kiểm tra trạng thái giao dịch.
        /// </summary>
        Task<PaymentStatusInfo> CheckStatusAsync(string transactionCode);
    }
}
