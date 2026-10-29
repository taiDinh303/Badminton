using System.Security.Cryptography;
using System.Text;
using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Time;
using Badminton.Core.Utils;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Operations;
using ModelViews.Operations;
namespace Services.Operations;

public sealed class CalendarSubscriptionService(IUnitOfWork uow, IOperationsAccess access, IBusinessClock clock) : ICalendarSubscriptionService
{
    public async Task<SubscriptionState> StateAsync()
    {
        await access.ActorAsync();
        var row = (await uow.GetRepository<CalendarSubscription>().GetByConditionAsync(x => x.UserId == access.ActorId)).SingleOrDefault();
        return new(row is { Revoked: false }, row?.LastFetchedAtUtc, row?.UpdatedAt ?? row?.CreatedAt);
    }
    public async Task<SubscriptionToken> RotateAsync()
    {
        await access.ActorAsync();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        await uow.RunInTransactionAsync(async () =>
        {
            await uow.LockNotificationAsync($"calendar-subscription:{access.ActorId}");
            var repo = uow.GetRepository<CalendarSubscription>();
            var row = (await repo.GetByConditionAsync(x => x.UserId == access.ActorId)).SingleOrDefault();
            if (row is null)
            {
                row = new()
                {
                    UserId = access.ActorId,
                    TokenHash = Hash(token)
                };
                await repo.AddAsync(row);
            }
            else
            {
                row = (await repo.GetFreshByIdAsync(row.Id))!;
                row.TokenHash = Hash(token);
                row.Revoked = false;
                row.LastFetchedAtUtc = null;
                row.UpdatedAt = clock.UtcNow;
                repo.Update(row);
            }
            await uow.SaveChangesAsync();
        });
        return new(token);
    }
    public Task RevokeAsync() => uow.RunInTransactionAsync(async () =>
    {
        await access.ActorAsync();
        await uow.LockNotificationAsync($"calendar-subscription:{access.ActorId}");
        var repo = uow.GetRepository<CalendarSubscription>();
        var row = (await repo.GetByConditionAsync(x => x.UserId == access.ActorId)).SingleOrDefault();
        if (row is not null)
        {
            row = (await repo.GetFreshByIdAsync(row.Id))!;
            row.Revoked = true;
            row.UpdatedAt = clock.UtcNow;
            repo.Update(row);
            await uow.SaveChangesAsync();
        }
    });
    public async Task<string> FeedAsync(string token)
    {
        if (token.Length != 64 || !token.All(Uri.IsHexDigit))
            throw new NotFoundException("Calendar", 0);
        var hash = Hash(token);
        var subscription = (await uow.GetRepository<CalendarSubscription>().GetByConditionAsync(x => x.TokenHash == hash && !x.Revoked)).SingleOrDefault() ?? throw new NotFoundException("Calendar", 0);
        var user = await uow.GetRepository<User>().GetByIdAsync(subscription.UserId);
        if (user is null || user.DeletedAt.HasValue || user.Status != UserStatus.Active || !user.IsVerified)
            throw new NotFoundException("Calendar", 0);
        await uow.RunInTransactionAsync(async () =>
        {
            await uow.LockNotificationAsync($"calendar-subscription:{subscription.UserId}");
            var fresh = (await uow.GetRepository<CalendarSubscription>().GetFreshByIdAsync(subscription.Id))!;
            if (fresh.Revoked || fresh.TokenHash != hash)
                throw new NotFoundException("Calendar", 0);
            fresh.LastFetchedAtUtc = clock.UtcNow;
            uow.GetRepository<CalendarSubscription>().UpdateProperties(fresh, nameof(CalendarSubscription.LastFetchedAtUtc));
            await uow.SaveChangesAsync();
        });
        // Keep cancelled events as tombstones, including old dates, so subscriptions can remove them.
        var bookings = await uow.GetRepository<Booking>().GetByConditionAsync(x => x.UserId == user.Id);
        var slots = (await uow.GetRepository<TimeSlot>().GetAllAsync()).ToDictionary(x => x.Id);
        var courts = (await uow.GetRepository<Court>().GetAllAsync()).ToDictionary(x => x.Id);
        var branches = (await uow.GetRepository<Branch>().GetAllAsync()).ToDictionary(x => x.Id);
        var lines = new List<string> { "BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//BadmintonBooking//Subscription//VI", "CALSCALE:GREGORIAN", "METHOD:PUBLISH", "X-WR-CALNAME:Lịch cầu lông", "REFRESH-INTERVAL;VALUE=DURATION:PT15M", "X-PUBLISHED-TTL:PT15M" };
        foreach (var b in bookings.OrderBy(x => x.Id))
        {
            if (!slots.TryGetValue(b.SlotId, out var slot))
                continue;
            var cancelled = b.DeletedAt.HasValue || b.Status is BookingStatus.Cancelled or BookingStatus.Refunded or BookingStatus.NoShow || (b.Status == BookingStatus.Pending && b.PendingExpiresAt <= clock.UtcNow);
            var modified = new[] { b.CreatedAt, b.UpdatedAt ?? b.CreatedAt, b.CancelledAt ?? b.CreatedAt, b.DeletedAt ?? b.CreatedAt }.Max();
            if (b.Status == BookingStatus.Pending && b.PendingExpiresAt is { } expired && expired <= clock.UtcNow && expired > modified)
                modified = expired;
            lines.AddRange(["BEGIN:VEVENT", $"UID:booking-{b.Id}@badmintonbooking", $"DTSTAMP:{modified:yyyyMMdd'T'HHmmss'Z'}", $"LAST-MODIFIED:{modified:yyyyMMdd'T'HHmmss'Z'}", $"DTSTART:{HanoiTime.SlotStart(b.Date, slot.StartTime).AddHours(-7):yyyyMMdd'T'HHmmss'Z'}", $"DTEND:{HanoiTime.SlotEnd(b.Date, slot.EndTime).AddHours(-7):yyyyMMdd'T'HHmmss'Z'}", "SUMMARY:" + Escape($"Cầu lông · {courts.GetValueOrDefault(b.CourtId)?.Name} · {b.BookingCode}"), "LOCATION:" + Escape(branches.GetValueOrDefault(b.BranchId)?.Address ?? ""), "STATUS:" + (cancelled ? "CANCELLED" : b.Status == BookingStatus.Pending ? "TENTATIVE" : "CONFIRMED"), "END:VEVENT"]);
        }
        lines.Add("END:VCALENDAR");
        return string.Join("\r\n", lines.Select(Fold)) + "\r\n";
    }
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\r", "").Replace("\n", "\\n").Replace(";", "\\;").Replace(",", "\\,");
    private static string Fold(string value)
    {
        var sb = new StringBuilder();
        var bytes = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            if (bytes + rune.Utf8SequenceLength > 73)
            {
                sb.Append("\r\n ");
                bytes = 1;
            }
            sb.Append(rune);
            bytes += rune.Utf8SequenceLength;
        }
        return sb.ToString();
    }
}
