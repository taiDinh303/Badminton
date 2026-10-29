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

public sealed class BookingCalendarService(IUnitOfWork uow, IOperationsAccess access, IBusinessClock clock,
    BookingAvailabilityService availability) : IBookingCalendarService
{
    public async Task<IReadOnlyList<FavoriteView>> CourtsAsync(int? branchId)
    {
        var branch = await access.StaffBranchAsync(branchId);
        return (await uow.GetRepository<Court>().GetByConditionAsync(c => c.DeletedAt == null && (branch == null || c.BranchId == branch)))
            .Select(c => new FavoriteView(c.Id, c.BranchId, c.Name)).ToArray();
    }
    public async Task<IReadOnlyList<CalendarEntry>> CalendarAsync(DateOnly from, DateOnly to, int? branchId)
    {
        OperationInput.DateRange(from, to);
        var branch = await access.StaffBranchAsync(branchId);
        var (bookings, total) = await uow.GetRepository<Booking>().GetPagedAsync(b => b.DeletedAt == null && b.Date >= from && b.Date <= to && (branch == null || b.BranchId == branch), b => b.CreatedAt, OrderDirection.Asc, new Page(1, 10000));
        if (total > 10000) throw new BadRequestException("Lịch có quá nhiều đơn; chọn ít ngày hơn hoặc một cơ sở cụ thể.");
        var courts = (await uow.GetRepository<Court>().GetByConditionAsync(c => branch == null || c.BranchId == branch)).ToDictionary(c => c.Id);
        var slots = (await uow.GetRepository<TimeSlot>().GetAllAsync()).ToDictionary(s => s.Id);
        var ids = bookings.Select(b => b.UserId).Distinct().ToArray();
        var users = (await uow.GetRepository<User>().GetByConditionAsync(u => ids.Contains(u.Id))).ToDictionary(u => u.Id);
        return bookings.OrderBy(b => b.Date).ThenBy(b => b.SlotId).Select(b => new CalendarEntry(b.Id, b.BookingCode, b.CourtId,
            courts.GetValueOrDefault(b.CourtId)?.Name ?? "", b.Date, slots[b.SlotId].StartTime.ToString("HH:mm"), slots[b.SlotId].EndTime.ToString("HH:mm"), b.Status.ToString(), b.CheckedInAt.HasValue, users.GetValueOrDefault(b.UserId)?.FullName ?? "")).ToArray();
    }
    public async Task<IReadOnlyList<HistoryView>> HistoryAsync(int id)
    {
        var b = await access.BookingAsync(id);
        var history = (await uow.GetRepository<BookingHistory>().GetByConditionAsync(h => h.BookingId == id)).Select(h => new HistoryView(h.Event, h.Detail, h.CreatedAt)).ToList();
        history.Add(new("created", "Đã tạo đơn đặt sân.", b.CreatedAt));
        if (b.CheckedInAt is { } checkedIn)
            history.Add(new("checked-in", "Nhân viên đã xác nhận khách đến.", checkedIn));
        if (b.CancelledAt is { } cancelled)
            history.Add(new("cancelled", b.CancelReason ?? "Đơn đã hủy.", cancelled));
        if (b.Status == BookingStatus.Completed)
            history.Add(new("completed", "Đơn đã hoàn thành.", b.UpdatedAt ?? b.CreatedAt));
        return history.OrderBy(h => h.At).ToArray();
    }
    public async Task<string> ICalendarAsync(int id)
    {
        var b = await access.BookingAsync(id);
        var slot = await uow.GetRepository<TimeSlot>().GetByIdAsync(b.SlotId) ?? throw new NotFoundException("TimeSlot", b.SlotId);
        var court = await uow.GetRepository<Court>().GetByIdAsync(b.CourtId);
        var branch = await uow.GetRepository<Branch>().GetByIdAsync(b.BranchId);
        var start = HanoiTime.SlotStart(b.Date, slot.StartTime).AddHours(-7);
        var end = HanoiTime.SlotEnd(b.Date, slot.EndTime).AddHours(-7);
        var status = b.Status is BookingStatus.Cancelled or BookingStatus.Refunded or BookingStatus.NoShow ? "CANCELLED" : "CONFIRMED";
        string[] lines = ["BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//BadmintonBooking//Booking//VI", "METHOD:PUBLISH", "BEGIN:VEVENT", $"UID:booking-{id}@badmintonbooking", $"DTSTAMP:{clock.UtcNow:yyyyMMdd'T'HHmmss'Z'}", $"LAST-MODIFIED:{(b.UpdatedAt ?? b.CreatedAt):yyyyMMdd'T'HHmmss'Z'}", $"DTSTART:{start:yyyyMMdd'T'HHmmss'Z'}", $"DTEND:{end:yyyyMMdd'T'HHmmss'Z'}", "SUMMARY:" + Escape($"Cầu lông · {court?.Name} · {b.BookingCode}"), "LOCATION:" + Escape(branch?.Address ?? ""), $"STATUS:{status}", "DESCRIPTION:" + Escape("Mở đơn trong ứng dụng để xem lịch mới nhất. Check-in từ trước giờ bắt đầu 30 phút đến hết slot."), "END:VEVENT", "END:VCALENDAR"];
        return string.Join("\r\n", lines.Select(Fold)) + "\r\n";
    }
    public async Task<IReadOnlyList<AlternativeSlot>> AlternativesAsync(int courtId, DateOnly date, int slotId)
    {
        await access.ActorAsync();
        if (date < clock.Today || date > clock.Today.AddDays(30))
            throw new BadRequestException("Chọn ngày trong 30 ngày tới.");
        var court = await uow.GetRepository<Court>().GetByIdAsync(courtId) ?? throw new NotFoundException("Court", courtId);
        var slot = await uow.GetRepository<TimeSlot>().GetByIdAsync(slotId) ?? throw new NotFoundException("TimeSlot", slotId);
        var choices = await availability.GetAsync(court.BranchId, date);
        return choices.SelectMany(c => c.Slots.Where(s => !s.IsLocked && (c.CourtId != courtId || s.SlotId != slotId))
            .Select(s => (Distance: Math.Abs(TimeOnly.Parse(s.StartTime).ToTimeSpan().TotalMinutes - slot.StartTime.ToTimeSpan().TotalMinutes), Value: new AlternativeSlot(c.CourtId, c.CourtName, s.SlotId, date, s.Label,
                s.IsPeak ? c.PricePeak : c.PriceLowPeak))))
            .OrderBy(x => x.Distance).ThenBy(x => x.Value.CourtId).Take(12).Select(x => x.Value).ToArray();
    }
    private static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\r", "").Replace("\n", "\\n").Replace(";", "\\;").Replace(",", "\\,");
    private static string Fold(string value)
    {
        var output = new StringBuilder();
        var bytes = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            if (bytes + rune.Utf8SequenceLength > 73)
            {
                output.Append("\r\n ");
                bytes = 1;
            }
            output.Append(rune.ToString());
            bytes += rune.Utf8SequenceLength;
        }
        return output.ToString();
    }
}
