using Badminton.Core.Time;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Operations;
namespace Services.Operations;

public sealed class ClubMaintenanceService(IUnitOfWork uow, IBusinessClock clock, IInboxService inbox, ICoachCourtService courts) : IClubMaintenanceService
{
    public async Task<int> RunAsync()
    {
        var changed = 0;
        var (sessions, _) = await uow.GetRepository<PlayerSession>().GetPagedAsync(x => !x.Closed && x.EndsAtUtc <= clock.UtcNow, x => x.EndsAtUtc, OrderDirection.Asc, new(1, 100));
        foreach (var candidate in sessions)
            await uow.RunInTransactionAsync(async () =>
            {
                await uow.LockNotificationAsync($"player-session:{candidate.Id}");
                var row = await uow.GetRepository<PlayerSession>().GetFreshByIdAsync(candidate.Id);
                if (row is null || row.Closed || row.EndsAtUtc > clock.UtcNow)
                    return;
                row.Closed = true;
                row.UpdatedAt = clock.UtcNow;
                uow.GetRepository<PlayerSession>().Update(row);
                await uow.SaveChangesAsync();
                changed++;
            });
        var (orders, _) = await uow.GetRepository<EquipmentOrder>().GetPagedAsync(x => (x.Status == "Reserved" && (x.PickupExpiresAtUtc ?? x.DueAtUtc) <= clock.UtcNow) || (x.DueAtUtc <= clock.UtcNow && x.Rental && x.Status == "Issued" && x.OverdueNotifiedAtUtc == null), x => x.DueAtUtc, OrderDirection.Asc, new(1, 100));
        foreach (var candidate in orders)
            await uow.RunInTransactionAsync(async () =>
            {
                await uow.LockNotificationAsync($"equipment:{candidate.ItemId}");
                var row = await uow.GetRepository<EquipmentOrder>().GetFreshByIdAsync(candidate.Id);
                if (row is null)
                    return;
                if (row.Status == "Reserved" && (row.PickupExpiresAtUtc ?? row.DueAtUtc) <= clock.UtcNow)
                {
                    row.Status = "Expired";
                    row.ExpiredAtUtc = clock.UtcNow;
                    row.UpdatedAt = clock.UtcNow;
                    uow.GetRepository<EquipmentOrder>().Update(row);
                    await uow.SaveChangesAsync();
                    changed++;
                    await inbox.PublishAsync(row.UserId, null, $"equipment-expired:{row.Id}", "Yêu cầu dụng cụ quá hẹn", "Yêu cầu chưa nhận đã hết hạn và nhả tồn kho. Bạn có thể đặt yêu cầu mới.");
                }
                else if (row.Rental && row.Status == "Issued" && row.DueAtUtc <= clock.UtcNow && row.OverdueNotifiedAtUtc == null)
                {
                    await inbox.PublishAsync(row.UserId, null, $"equipment-overdue:{row.Id}", "Dụng cụ đến hạn trả", $"Yêu cầu #{row.Id} đã quá hạn trả. Liên hệ cơ sở để trả hoặc báo hư/mất; không tự áp phí phạt.");
                    row.OverdueNotifiedAtUtc = clock.UtcNow;
                    uow.GetRepository<EquipmentOrder>().Update(row);
                    await uow.SaveChangesAsync();
                }
            });
        return changed + await courts.ReconcileAsync();
    }
}
