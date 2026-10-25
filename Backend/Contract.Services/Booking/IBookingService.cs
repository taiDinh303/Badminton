using Contract.Repositories.UnitOfWork;
using ModelViews.Booking;
using ModelViews.Court;

namespace Contract.Services.Booking
{
    public interface IBookingService
    {
        Task<BookingView> CreateAsync(CreateBookingRequest request, int userId);
        Task<BookingPriceBreakdown> QuoteAsync(CreateBookingRequest request, int userId);
        Task<BookingView> GetByIdAsync(int id, int requestingUserId);
        Task<IReadOnlyList<BookingView>> GetByUserAsync(int userId, string? status = null);

        Task<(IReadOnlyList<BookingView> Items, int TotalCount)>
            GetByUserPagedAsync(int userId, string? status, Page page);

        Task<(IReadOnlyList<BookingView> Items, int TotalCount)>
            GetAllPagedAsync(string? status, int? branchId, Page page, string? search = null, DateOnly? date = null, bool needsAttendance = false);

        Task CancelForMaintenanceAsync(int bookingId, int actorId);
        Task<BookingView> CancelAsync(int bookingId, int actorId);
        Task<IReadOnlyList<CourtAvailabilityView>> GetAvailabilityAsync(int branchId, DateOnly date);

        Task<BookingView> MarkCompletedAsync(int bookingId, int actorId, string? note = null);

        Task<BookingView> MarkNoShowAsync(int bookingId, int actorId, string? note = null);
    }
}
