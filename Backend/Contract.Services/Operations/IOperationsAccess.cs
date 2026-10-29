using Contract.Repositories.Entity;
using BookingEntity = Contract.Repositories.Entity.Booking;
namespace Contract.Services.Operations;

public interface IOperationsAccess
{
    int ActorId
    {
        get;
    }
    Task<User> ActorAsync();
    Task<int?> StaffBranchAsync(int? requested = null);
    Task<BookingEntity> BookingAsync(int id, bool ownerOrStaff = true);
    Task<BookingEntity> StaffBookingAsync(int id);
}
