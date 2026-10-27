using Badminton.Core.Enums;
using Badminton.Core.Time;
using Badminton.Core.Utils;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Operations;
namespace Services.Operations;

public sealed class ReminderService(IUnitOfWork uow, IBusinessClock clock, IDurableNotificationQueue queue) : IReminderService
{
    public async Task<int> RunAsync()
    {
        var tomorrow = clock.Today.AddDays(1);
        var bookings = await uow.GetRepository<Booking>().GetByConditionAsync(b => b.DeletedAt == null && b.Status == BookingStatus.Confirmed && b.CheckedInAt == null && b.Date >= clock.Today && b.Date <= tomorrow);
        var slots = (await uow.GetRepository<TimeSlot>().GetAllAsync()).ToDictionary(s => s.Id);
        var ids = bookings.Select(b => b.UserId).Distinct().ToArray();
        var preferences = (await uow.GetRepository<NotificationPreference>().GetByConditionAsync(p => ids.Contains(p.UserId))).ToDictionary(p => p.UserId);
        var count = 0;
        foreach (var booking in bookings)
        {
            if (!slots.TryGetValue(booking.SlotId, out var slot))
                continue;
            var preference = preferences.GetValueOrDefault(booking.UserId);
            if (preference?.RemindersEnabled == false)
                continue;
            var start = HanoiTime.SlotStart(booking.Date, slot.StartTime);
            if (start <= clock.LocalNow || start > clock.LocalNow.AddMinutes(preference?.MinutesBefore ?? 60))
                continue;
            var key = $"reminder:{booking.Id}:{booking.Date:yyyyMMdd}:{booking.SlotId}";
            var alreadyQueued = await uow.GetRepository<InboxMessage>().CountByConditionAsync(m => m.UserId == booking.UserId && m.EventKey == key) > 0;
            var channels = new List<string>();
            if (preference?.Email == true)
                channels.Add("email");
            if (preference?.Sms == true)
                channels.Add("sms");
            if (preference?.Zalo == true)
                channels.Add("zalo");
            await queue.QueueAsync(NotifyEvent.BookingReminder, booking.UserId, booking.Id,
                $"Lịch {booking.BookingCode}: {booking.Date:dd/MM/yyyy} {slot.StartTime:HH:mm}–{slot.EndTime:HH:mm}. Mang mã đặt/QR đến sân để check-in.", key, channels.ToArray());
            if (!alreadyQueued)
                count++;
        }
        return count;
    }
}
