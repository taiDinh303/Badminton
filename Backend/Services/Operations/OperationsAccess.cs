using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Security;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Operations;
namespace Services.Operations;

public sealed class OperationsAccess(IUnitOfWork uow, ICurrentUser currentUser) : IOperationsAccess
{
    public int ActorId => currentUser.Id;
    public async Task<User> ActorAsync()
    {
        var user = await uow.GetRepository<User>().GetByIdAsync(ActorId);
        if (user is null || user.DeletedAt.HasValue || !user.IsVerified || user.Status != UserStatus.Active || !currentUser.IsInRole(user.Role.ToString()))
            throw new ConflictException("Tài khoản không còn quyền thao tác.");
        return user;
    }
    public async Task<int?> StaffBranchAsync(int? requested = null)
    {
        var actor = await ActorAsync();
        if (actor.Role == Role.Admin)
            return requested;
        if (actor.Role != Role.Manager || actor.BranchId is null || (requested.HasValue && actor.BranchId != requested))
            throw new ConflictException("Bạn không có quyền xử lý cơ sở này.");
        return actor.BranchId;
    }
    public async Task<Booking> BookingAsync(int id, bool ownerOrStaff = true)
    {
        var actor = await ActorAsync();
        var booking = await uow.GetRepository<Booking>().GetByIdAsync(id);
        if (booking is null || booking.DeletedAt.HasValue)
            throw new NotFoundException("Booking", id);
        if (booking.UserId == actor.Id)
            return booking;
        if (ownerOrStaff)
        {
            await StaffBranchAsync(booking.BranchId);
            return booking;
        }
        throw new ConflictException("Chỉ người đặt được thực hiện thao tác này.");
    }
    public async Task<Booking> StaffBookingAsync(int id)
    {
        var booking = await uow.GetRepository<Booking>().GetByIdAsync(id);
        if (booking is null || booking.DeletedAt.HasValue)
            throw new NotFoundException("Booking", id);
        await StaffBranchAsync(booking.BranchId);
        return booking;
    }
}
