using ModelViews.Booking;

namespace Contract.Services.Interface
{
    public interface IBookingService
    {
        Task<BookingResponse> CreateAsync(
            CreateBookingRequest request,
            int userId);

        Task<BookingResponse?> GetByIdAsync(
            int bookingId,
            int userId,
            string role);

        Task<List<BookingResponse>> GetByUserIdAsync(int userId);

        Task<List<BookingResponse>> GetAllAsync();

        Task<List<AvailableCourtResponse>> GetAvailableAsync(
            DateTime bookingDate);

        Task<bool> ConfirmAsync(
            int bookingId,
            int userId,
            string role);

        Task<bool> CancelAsync(
            int bookingId,
            int userId,
            string role);

        Task<bool> CompleteAsync(
            int bookingId,
            int userId,
            string role);

        Task<List<BookingResponse>> GetMyBookingsAsync(
            int userId);
    }
}