using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Security;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Booking;

namespace Services;

public sealed class BookingCancellationAuthorization(IUnitOfWork uow, ICurrentUser currentUser)
    : BaseService(uow, currentUser), IBookingCancellationAuthorization
{
    public async Task EnsureAuthorizedAsync(Booking booking, int actorId)
    {
        if (_currentUser.Id != actorId) throw new ConflictException("Danh tính thao tác không hợp lệ.");
        var actor = (await Repository<User>().GetByConditionAsync(u => u.Id == actorId)).SingleOrDefault();
        if (actor is null || actor.DeletedAt.HasValue || actor.Status != UserStatus.Active || !actor.IsVerified
            || !_currentUser.IsInRole(actor.Role.ToString()))
            throw new ConflictException("Tài khoản không còn quyền hủy đơn.");
        if (booking.UserId == actorId) return;
        if (booking.GroupId is { } groupId)
        {
            await Uow.LockGroupAsync(groupId);
            var group = (await Repository<PlayGroup>().GetByConditionAsync(g => g.Id == groupId)).SingleOrDefault();
            if (group is { DeletedAt: null } && group.OwnerId == actorId) return;
        }
        await AuthorizeStaffBranchAsync(booking.BranchId, actorId);
    }
}
