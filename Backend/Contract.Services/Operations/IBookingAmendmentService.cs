using ModelViews.Booking;
using ModelViews.Operations;
namespace Contract.Services.Operations;

public interface IBookingAmendmentService
{
    Task RescheduleAsync(int id, RescheduleRequest request);
}
