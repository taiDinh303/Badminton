using ModelViews.Booking;
using ModelViews.Operations;
namespace Contract.Services.Operations;

public interface IBookingCaseService
{
    Task<int> CreateAsync(int bookingId, BookingCaseRequest request);
    Task<IReadOnlyList<CaseView>> ListAsync(int? bookingId, int? branchId, int page);
    Task ResolveAsync(int id, ResolutionRequest request);
}
