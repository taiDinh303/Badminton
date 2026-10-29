using ModelViews.Booking;
using ModelViews.Operations;
namespace Contract.Services.Operations;

public interface IBookingCalendarService
{
    Task<IReadOnlyList<FavoriteView>> CourtsAsync(int? branchId);
    Task<IReadOnlyList<CalendarEntry>> CalendarAsync(DateOnly from, DateOnly to, int? branchId);
    Task<IReadOnlyList<HistoryView>> HistoryAsync(int id);
    Task<string> ICalendarAsync(int id);
    Task<IReadOnlyList<AlternativeSlot>> AlternativesAsync(int courtId, DateOnly date, int slotId);
}
