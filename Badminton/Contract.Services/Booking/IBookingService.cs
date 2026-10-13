using ModelViews.Booking;
using ModelViews.Court;

namespace Contract.Services.Booking
{
    public interface IBookingService
    {
        Task<BookingView> CreateAsync(CreateBookingRequest request, int userId);
        Task<BookingView> GetByIdAsync(int id, int requestingUserId);
        Task<IReadOnlyList<BookingView>> GetByUserAsync(int userId, string? status = null);
        Task<BookingView> CancelAsync(int bookingId, int actorId);
        Task<IReadOnlyList<CourtAvailabilityView>> GetAvailabilityAsync(int branchId, DateOnly date);
    }
}
