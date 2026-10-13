using ModelViews.Payment;

namespace Contract.Services.Payment
{
    public interface IPaymentService
    {
        Task<PaymentView> ConfirmPaymentAsync(ConfirmPaymentRequest request, int userId, string userName);
        Task RefundAsync(int paymentId, string userName);
        Task<PaymentView> GetByBookingAsync(int bookingId);
    }
}
