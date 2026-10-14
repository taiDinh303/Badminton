using Contract.Repositories.UnitOfWork;
using ModelViews.Booking;
using ModelViews.Court;

namespace Contract.Services.Booking
{
    public interface IBookingService
    {
        Task<BookingView> CreateAsync(CreateBookingRequest request, int userId);
        Task<BookingView> GetByIdAsync(int id, int requestingUserId);
        Task<IReadOnlyList<BookingView>> GetByUserAsync(int userId, string? status = null);

        /// <summary>Paged "my bookings", server-side Skip/Take + total.</summary>
        Task<(IReadOnlyList<BookingView> Items, int TotalCount)>
            GetByUserPagedAsync(int userId, string? status, Page page);

        Task<BookingView> CancelAsync(int bookingId, int actorId);
        Task<IReadOnlyList<CourtAvailabilityView>> GetAvailabilityAsync(int branchId, DateOnly date);
    }
}
