using Contract.Repositories.Entity;

namespace Contract.Repositories.IOUW
{
    public interface IPaymentRepository
    {
        Task<Payment?> GetByBookingIdAsync(int bookingId);

        Task<Payment> CreateAsync(Payment payment);

        Task<bool> MarkAsPaidAsync(
            int bookingId,
            string transactionCode);
    }
}