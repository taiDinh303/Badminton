using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Security;
using Badminton.Core.Utils;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Pricing;
using ModelViews.Booking;

namespace Services;

public sealed class BookingAvailabilityService(IUnitOfWork uow, ICurrentUser currentUser, IPricingService pricing)
    : BaseService(uow, currentUser)
{
    private readonly IPricingService _pricing = pricing;
    private IGenericRepository<Booking> Repo => Repository<Booking>();
    public async Task<IReadOnlyList<CourtAvailabilityView>> GetAsync(int branchId, DateOnly date)
    {
        var branch = await Repository<Branch>().GetByIdAsync(branchId);
        if (branch is null) throw new NotFoundException("Branch", branchId);
        if (!branch.IsActive || branch.DeletedAt.HasValue || branch.Status != BranchStatus.Active) return [];
        var courts = await Repository<Court>().GetByConditionAsync(
            c => c.BranchId == branchId && c.IsActive && c.DeletedAt == null);
        var slots = await Repository<TimeSlot>().GetByConditionAsync(
            s => s.IsActive && s.DeletedAt == null);
        var hanoiNowAvail = HanoiTime.HanoiNow();

        var courtIds = courts.Select(c => c.Id).ToList();
        var dateStatusRows = await Repository<CourtDateStatusEntry>().GetByConditionAsync(
            r => r.Date == date && courtIds.Contains(r.CourtId));
        var statusByCourt = dateStatusRows
            .ToDictionary(r => r.CourtId);

        var bookedRows = await Repo.GetByConditionAsync(b =>
            b.Date == date && courtIds.Contains(b.CourtId)
            && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
        var bookedIds = bookedRows.Select(b => b.SlotId).Distinct().ToList();
        var bookedSlotTimes = (await Repository<TimeSlot>().GetByConditionAsync(s => bookedIds.Contains(s.Id))).ToDictionary(s => s.Id);
        var bookedByCourt = bookedRows
            .GroupBy(b => b.CourtId)
            .ToDictionary(g => g.Key, g => g.Select(b => b.SlotId).ToHashSet());

        var views = new List<CourtAvailabilityView>(courts.Count);
        foreach (var c in courts)
        {
            statusByCourt.TryGetValue(c.Id, out var schedule);
            var bookedSlots = bookedByCourt.TryGetValue(c.Id, out var set) ? set : default;
            bookedSlots ??= new HashSet<int>();

            var slotViews = slots.Where(sl => sl.StartTime >= branch.OpenTime && sl.EndTime <= branch.CloseTime)
                .OrderBy(sl => sl.StartTime).Select(sl =>
            {
                var start = HanoiTime.SlotStart(date, sl.StartTime);
                var dayStatus = schedule?.Blocks(sl.StartTime, sl.EndTime) == true ? schedule.Status : CourtDateStatus.Open;
                var overlaps = bookedSlots.Any(id => bookedSlotTimes.TryGetValue(id, out var other) && other.StartTime < sl.EndTime && sl.StartTime < other.EndTime);
                var isLocked = dayStatus != CourtDateStatus.Open
                    || overlaps
                    || start < hanoiNowAvail.AddMinutes(30);
                var lockReason = isLocked
                    ? (dayStatus != CourtDateStatus.Open ? (dayStatus == CourtDateStatus.Maintenance ? "Bảo trì" : "Đóng")
                      : overlaps ? "Đã đặt"
                      : "Đã qua giờ đặt")
                    : "";
                return new SlotView(sl.Id, sl.Label,
                    sl.StartTime.ToString("HH\\:mm"), sl.EndTime.ToString("HH\\:mm"),
                    sl.IsPeak, isLocked, lockReason);
            }).ToList();

            views.Add(new CourtAvailabilityView(
                c.Id, c.Code, c.Name,
                (int)_pricing.WithWeekend(c.PriceLowPeak, c.PricePeak, false, c.WeekendSurcharge, (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)),
                (int)_pricing.WithWeekend(c.PriceLowPeak, c.PricePeak, true, c.WeekendSurcharge, (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)), slotViews, c.Capacity));
        }
        return views;
    }

}
