using ModelViews.Booking;

namespace Contract.Services.Interface
{
    public interface IBookingDetailService
    {
        Task<BookingDetailResponse?> GetByIdAsync(
            int bookingDetailId,
            int userId,
            string role);

        Task<List<BookingDetailResponse>?> GetByBookingIdAsync(
            int bookingId,
            int userId,
            string role);

        Task<bool> CancelAsync(
            int bookingDetailId,
            int userId,
            string role);
    }
}