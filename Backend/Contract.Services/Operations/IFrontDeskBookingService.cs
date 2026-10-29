using ModelViews.Booking;
using ModelViews.Operations;
namespace Contract.Services.Operations;

public interface IFrontDeskBookingService
{
    Task<DeskCustomerView?> FindCustomerAsync(string phone);
    Task<BookingView> CreateForCustomerAsync(DeskBookingRequest request);
}
