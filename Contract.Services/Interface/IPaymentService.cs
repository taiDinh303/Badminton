using ModelViews.Payment;

namespace Contract.Services.Interface
{
    public interface IPaymentService
    {
        Task<PaymentResponse?> CreateAsync(
            int bookingId,
            int userId,
            string role,
            string paymentMethod);

        Task<PaymentResponse?> GetByBookingIdAsync(
            int bookingId,
            int userId,
            string role);

        Task<bool> MarkAsPaidAsync(
            int bookingId,
            int userId,
            string role,
            string transactionCode);
    }
}