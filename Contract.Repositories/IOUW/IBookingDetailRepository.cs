using Contract.Repositories.Entity;

namespace Contract.Repositories.IOUW
{
    public interface IBookingDetailRepository
    {
        Task<BookingDetail?> GetByIdAsync(int bookingDetailId);

        Task<List<BookingDetail>> GetByBookingIdAsync(int bookingId);

        Task<bool> CancelAsync(int bookingDetailId);
    }
}