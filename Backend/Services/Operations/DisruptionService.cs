using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Badminton.Core.Base;
using Badminton.Core.Enums;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Operations;
using ModelViews.Operations;
namespace Services.Operations;

public sealed class DisruptionService(IUnitOfWork uow, IOperationsAccess access, IDurableNotificationQueue queue, IAuditService audit) : IDisruptionService
{
    public async Task<int> AnnounceAsync(IncidentRequest request)
    {
        await access.StaffBranchAsync(request.BranchId);
        var message = OperationInput.Text(request.Message, 1500);
        var key = OperationInput.Text(request.RequestKey, 60, 1);
        var branch = await uow.GetRepository<Branch>().GetByIdAsync(request.BranchId) ?? throw new NotFoundException("Branch", request.BranchId);
        if (branch.DeletedAt.HasValue)
            throw new NotFoundException("Branch", request.BranchId);
        if (request.CourtId is { } courtId)
        {
            var court = await uow.GetRepository<Court>().GetByIdAsync(courtId);
            if (court?.BranchId != request.BranchId)
                throw new BadRequestException("Sân không thuộc cơ sở.");
        }
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request with
        {
            Message = message,
            RequestKey = key
        }))));
        var created = 0;
        await uow.RunInTransactionAsync(async () =>
        {
            await uow.LockBookingRequestAsync($"incident:{request.BranchId}:{key}");
            var repo = uow.GetRepository<DisruptionContact>();
            var existing = await repo.GetByConditionAsync(c => c.BranchId == request.BranchId && c.IncidentKey == key);
            if (existing.Count > 0)
            {
                if (existing.Any(c => c.Fingerprint != fingerprint))
                    throw new ConflictException("Mã thông báo đã dùng cho nội dung khác.");
                return;
            }
            var bookings = await uow.GetRepository<Booking>().GetByConditionAsync(b => b.DeletedAt == null && b.BranchId == request.BranchId && b.Date == request.Date
                && (request.CourtId == null || b.CourtId == request.CourtId) && (request.SlotId == null || b.SlotId == request.SlotId)
                && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
            if (bookings.Count > 500)
                throw new BadRequestException("Chọn phạm vi tối đa 500 đơn mỗi thông báo.");
            foreach (var booking in bookings)
            {
                await repo.AddAsync(new()
                {
                    BookingId = booking.Id,
                    BranchId = request.BranchId,
                    IncidentKey = key,
                    Fingerprint = fingerprint,
                    Message = message
                });
                var preference = (await uow.GetRepository<NotificationPreference>().GetByConditionAsync(p => p.UserId == booking.UserId)).SingleOrDefault();
                var channels = new List<string>();
                if (preference?.Email == true)
                    channels.Add("email");
                if (preference?.Sms == true)
                    channels.Add("sms");
                if (preference?.Zalo == true)
                    channels.Add("zalo");
                await queue.QueueAsync(NotifyEvent.BranchIncident, booking.UserId, booking.Id, $"{branch.Name} · {booking.BookingCode}: {message}", $"incident:{request.BranchId}:{key}:{booking.Id}", channels.ToArray());
                created++;
            }
            await uow.SaveChangesAsync();
            await audit.LogAsync(access.ActorId, "branch.incident-announced", "Branch", request.BranchId.ToString(), null, new
            {
                request.Date,
                request.CourtId,
                Count = created
            }, message);
        });
        return created;
    }
    public async Task<IReadOnlyList<DisruptionView>> ListAsync(int? branchId, int page)
    {
        var branch = await access.StaffBranchAsync(branchId);
        var (rows, _) = await uow.GetRepository<DisruptionContact>().GetPagedAsync(c => branch == null || c.BranchId == branch, c => c.CreatedAt, OrderDirection.Desc, new Page(page, 30));
        var ids = rows.Select(c => c.BookingId).ToArray();
        var bookings = (await uow.GetRepository<Booking>().GetByConditionAsync(b => ids.Contains(b.Id))).ToDictionary(b => b.Id);
        var users = bookings.Values.Select(b => b.UserId).Distinct().ToArray();
        var names = (await uow.GetRepository<User>().GetByConditionAsync(u => users.Contains(u.Id))).ToDictionary(u => u.Id, u => u.FullName);
        return rows.Select(c => new DisruptionView(c.Id, c.BookingId, bookings[c.BookingId].BookingCode, names.GetValueOrDefault(bookings[c.BookingId].UserId) ?? "", c.Message, c.ContactedAt, c.Outcome)).ToArray();
    }
    public async Task RecordContactAsync(int id, ContactOutcomeRequest request)
    {
        var outcome = OperationInput.Text(request.Outcome, 1500);
        await uow.RunInTransactionAsync(async () =>
        {
            await uow.LockBookingRequestAsync($"disruption-contact:{id}");
            var repo = uow.GetRepository<DisruptionContact>();
            var row = await repo.GetByIdAsync(id) ?? throw new NotFoundException("DisruptionContact", id);
            await access.StaffBranchAsync(row.BranchId);
            row.Outcome = outcome;
            row.ContactedAt = DateTime.UtcNow;
            row.ContactedBy = access.ActorId;
            repo.UpdateProperties(row, nameof(DisruptionContact.Outcome), nameof(DisruptionContact.ContactedAt), nameof(DisruptionContact.ContactedBy));
            await uow.SaveChangesAsync();
            await audit.LogAsync(access.ActorId, "incident.contact-recorded", "DisruptionContact", id.ToString(), null, null, outcome);
        });
    }
}
