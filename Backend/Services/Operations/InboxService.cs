using Badminton.Core.Base;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Operations;
using ModelViews.Operations;
namespace Services.Operations;

public sealed class InboxService(IUnitOfWork uow, IOperationsAccess access) : IInboxService
{
    public async Task<int> UnreadCountAsync()
    {
        await access.ActorAsync();
        return await uow.GetRepository<InboxMessage>().CountByConditionAsync(m => m.UserId == access.ActorId && m.ReadAt == null);
    }
    public async Task<IReadOnlyList<InboxView>> ListAsync(int page)
    {
        await access.ActorAsync();
        var (rows, _) = await uow.GetRepository<InboxMessage>().GetPagedAsync(m => m.UserId == access.ActorId, m => m.CreatedAt, OrderDirection.Desc, new Page(page, 30));
        return rows.Select(m => new InboxView(m.Id, m.BookingId, m.Title, m.Body, m.CreatedAt, m.ReadAt)).ToArray();
    }
    public async Task ReadAsync(int id)
    {
        await access.ActorAsync();
        var repo = uow.GetRepository<InboxMessage>();
        var row = await repo.GetByIdAsync(id);
        if (row is null || row.UserId != access.ActorId)
            throw new NotFoundException("InboxMessage", id);
        row.ReadAt ??= DateTime.UtcNow;
        repo.UpdateProperties(row, nameof(InboxMessage.ReadAt));
        await uow.SaveChangesAsync();
    }
    public async Task<PreferenceView> PreferencesAsync()
    {
        await access.ActorAsync();
        var row = (await uow.GetRepository<NotificationPreference>().GetByConditionAsync(p => p.UserId == access.ActorId)).SingleOrDefault();
        return row is null ? new(true, 60, false, false, false) : new(row.RemindersEnabled, row.MinutesBefore, row.Email, row.Sms, row.Zalo);
    }
    public async Task SetPreferencesAsync(PreferenceView request)
    {
        await access.ActorAsync();
        if (request.MinutesBefore is < 15 or > 1440)
            throw new BadRequestException("Thời gian nhắc từ 15 đến 1.440 phút trước giờ chơi.");
        await uow.RunInTransactionAsync(async () =>
        {
            await uow.LockPointsUserAsync(access.ActorId);
            var repo = uow.GetRepository<NotificationPreference>();
            var row = (await repo.GetByConditionAsync(p => p.UserId == access.ActorId)).SingleOrDefault();
            if (row is null)
            {
                row = new()
                {
                    UserId = access.ActorId
                };
                await repo.AddAsync(row);
            }
            else
                repo.Update(row);
            row.RemindersEnabled = request.RemindersEnabled;
            row.MinutesBefore = request.MinutesBefore;
            row.Email = request.Email;
            row.Sms = request.Sms;
            row.Zalo = request.Zalo;
            row.UpdatedAt = DateTime.UtcNow;
            await uow.SaveChangesAsync();
        });
    }
    public Task PublishAsync(int userId, int? bookingId, string eventKey, string title, string body)
        => uow.RunInTransactionAsync(async () =>
        {
            await uow.LockNotificationAsync($"inbox:{userId}:{eventKey}");
            var repo = uow.GetRepository<InboxMessage>();
            if (await repo.CountByConditionAsync(m => m.UserId == userId && m.EventKey == eventKey) > 0)
                return;
            await repo.AddAsync(new()
            {
                UserId = userId,
                BookingId = bookingId,
                EventKey = eventKey,
                Title = title,
                Body = body
            });
            await uow.SaveChangesAsync();
        });
}
