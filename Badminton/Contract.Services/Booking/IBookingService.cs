using ModelViews.Booking;

namespace Contract.Services.Booking
{
    public interface IBookingService
    {
        Task<BookingView> CreateAsync(CreateBookingRequest request, int userId, string userName);
        Task<IReadOnlyList<BookingView>> GetByUserAsync(int userId);
        Task<BookingView> GetByIdAsync(int id, int? userId);
        Task CancelAsync(int id, int userId, string userName);
        Task<IReadOnlyList<CourtSlotAvailabilityView>> GetFreeCourtsAsync(DateOnly date);
    }
}
