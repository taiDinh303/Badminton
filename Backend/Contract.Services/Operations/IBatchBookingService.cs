using ModelViews.Booking;
using ModelViews.Operations;
namespace Contract.Services.Operations;

public interface IBatchBookingService
{
    Task<IReadOnlyList<BookingView>> CreateAsync(BatchBookingRequest request);
}
