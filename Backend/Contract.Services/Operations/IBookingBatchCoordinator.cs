using ModelViews.Booking;
using ModelViews.Operations;
namespace Contract.Services.Operations;

public interface IBookingBatchCoordinator
{
    Task<IReadOnlyList<BookingView>> CreateAsync(CreateBookingRequest[] requests, int userId, string key, int? expectedTotal = null);
}
