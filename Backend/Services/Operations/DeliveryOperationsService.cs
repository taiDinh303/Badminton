using Badminton.Core.Base;
using Badminton.Core.Enums;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Operations;
using ModelViews.Operations;
namespace Services.Operations;

public sealed class DeliveryOperationsService(IUnitOfWork uow, IOperationsAccess access, IDurableNotificationQueue queue, IAuditService audit, IOperationsReadStore reads) : IDeliveryOperationsService
{
    public async Task<IReadOnlyList<DeliveryView>> ListAsync(int? branchId, int page)
    {
        var branch = await access.StaffBranchAsync(branchId);
        var rows = await reads.DeliveriesAsync(branch, new Page(page, 30));
        return rows.Select(n => new DeliveryView(n.Id, n.BookingId, n.Channel, n.Status.ToString(), n.LastError, n.CreatedAt, n.UpdatedAt, n.Attempts)).ToArray();
    }
    public async Task RetryAsync(int id)
    {
        var repo = uow.GetRepository<Notification>();
        var row = (await repo.GetByConditionAsync(n => n.Id == id)).SingleOrDefault() ?? throw new NotFoundException("Notification", id);
        if (row.BookingId is { } bookingId)
            await access.StaffBookingAsync(bookingId);
        else
        {
            if ((await access.ActorAsync()).Role != Role.Admin)
                throw new ConflictException("Chỉ Admin được xử lý thông báo ngoài đơn.");
        }
        await uow.RunInTransactionAsync(async () =>
        {
            await uow.LockNotificationAsync(row.LedgerKey);
            var fresh = await repo.GetByIdAsync(id) ?? throw new NotFoundException("Notification", id);
            if (fresh.Status == NotifyStatus.Delivered)
                throw new ConflictException("Thông báo đã được gửi thành công.");
            if (fresh.UpdatedAt > DateTime.UtcNow.AddSeconds(-30))
                throw new ConflictException("Vui lòng chờ 30 giây giữa các lần thử.");
            fresh.Status = NotifyStatus.Pending;
            repo.UpdateProperties(fresh, nameof(Notification.Status));
            await uow.SaveChangesAsync();
            await audit.LogAsync(access.ActorId, "notification.retry", "Notification", id.ToString(), null, new
            {
                fresh.Channel
            });
        });
        await queue.DeliverOneAsync(id);
    }
}
