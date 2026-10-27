using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Booking;
using Contract.Services.Notify;
using Contract.Services.Waitlist;
using Badminton.Core.Enums;
using ModelViews.Booking;
using Badminton.Core.Utils;

namespace Services
{
    public sealed class WaitlistService : BaseService, IWaitlistService
    {
        private readonly INotifyService _notify;
        private readonly IBookingService _booking;

        public WaitlistService(IUnitOfWork uow,
            Badminton.Core.Security.ICurrentUser currentUser,
            INotifyService notify,
            IBookingService booking)
            : base(uow, currentUser)
        {
            _notify = notify;
            _booking = booking;
        }

        private IGenericRepository<WaitlistEntry> WRepo => Repository<WaitlistEntry>();

        private static WaitlistEntryView ToView(WaitlistEntry w, string court,
            string slot) =>
            new(w.Id, w.CourtId, court, slot, w.Date, w.PlayerCount,
                w.Status, w.CreatedAt, w.PromotedBookingId, w.DeclineReason);

        public async Task<WaitlistEntryView> JoinAsync(int userId, int courtId,
            int slotId, DateOnly date, int playerCount, string? note)
        {
            var court = await Repository<Court>().GetByIdAsync(courtId);
            var slot = await Repository<TimeSlot>().GetByIdAsync(slotId);
            if (court is null || slot is null)
                throw new Badminton.Core.Base.NotFoundException("Court or Slot", courtId);

            if (playerCount < 1 || playerCount > court.Capacity) throw new Badminton.Core.Base.BadRequestException($"Số khách phải từ 1 đến {court.Capacity} (sức chứa sân).");
            if (!court.IsActive || court.DeletedAt.HasValue || !slot.IsActive || slot.DeletedAt.HasValue) throw new Badminton.Core.Base.BadRequestException("Sân/khung giờ không hoạt động.");

            // The slot must still be in the future (Hanoi business clock).
            var startHanoi = Badminton.Core.Utils.HanoiTime.SlotStart(date, slot.StartTime);
            if (startHanoi < Badminton.Core.Utils.HanoiTime.HanoiNow().AddMinutes(30))
                throw new Badminton.Core.Base.BadRequestException(
                    "Slot này đã qua giờ bắt đầu, không thể chờ.");

            if (await CountOverlappingBookingsAsync(courtId, date, slot) == 0)
                throw new Badminton.Core.Base.BadRequestException(
                    "Slot còn trống, không cần danh sách chờ — hãy đặt trực tiếp.");

            var dup = await WRepo.CountByConditionAsync(w =>
                w.UserId == userId && w.CourtId == courtId &&
                w.SlotId == slotId && w.Date == date
                && w.Status == WaitlistStatus.Waiting);
            if (dup > 0)
                throw new Badminton.Core.Base.ConflictException(
                    "Bạn đã nằm trong danh sách chờ khung này rồi.");

            var e = new WaitlistEntry
            {
                UserId = userId,
                CourtId = courtId,
                SlotId = slotId,
                BranchId = court.BranchId,
                Date = date,
                PlayerCount = playerCount,
                Note = note,
                Status = WaitlistStatus.Waiting,
                CreatedAt = DateTime.UtcNow
            };
            await WRepo.AddAsync(e);
            await SaveAsync();
            return ToView(e, court.Name, slot.Label);
        }

        public async Task<WaitlistEntryView> LeaveAsync(int entryId, int userId)
        {
            WaitlistEntryView result = null!;
            await Uow.RunInTransactionAsync(async () => {
                await Uow.LockBookingRequestAsync($"waitlist-finish:{entryId}");
                result = await LeaveCoreAsync(entryId, userId);
            });
            return result;
        }

        private async Task<WaitlistEntryView> LeaveCoreAsync(int entryId, int userId)
        {
            var e = (await WRepo.GetByConditionAsync(w => w.Id == entryId)).SingleOrDefault();
            if (e is null || e.UserId != userId)
                throw new Badminton.Core.Base.NotFoundException("WaitlistEntry", entryId);
            if (e.Status != WaitlistStatus.Waiting)
                throw new Badminton.Core.Base.ConflictException(
                    "Bản chờ này đã được xử lý, không thể rời.");
            e.Status = WaitlistStatus.Cancelled;
            e.ResolvedAt = DateTime.UtcNow;
            WRepo.Update(e);
            await SaveAsync();
            var court = await Repository<Court>().GetByIdAsync(e.CourtId);
            var slot = await Repository<TimeSlot>().GetByIdAsync(e.SlotId);
            return ToView(e, court?.Name ?? "", slot?.Label ?? "");
        }

        public async Task<IReadOnlyList<WaitlistEntryView>> GetMineAsync(int userId)
        {
            var rows = await WRepo.GetByConditionAsync(w => w.UserId == userId);
            var courts = rows.Select(r => r.CourtId).Distinct().ToList();
            var slots = rows.Select(r => r.SlotId).Distinct().ToList();
            var cMap = (await Repository<Court>().GetByConditionAsync(c => courts.Contains(c.Id)))
                .ToDictionary(c => c.Id, c => c.Name);
            var sMap = (await Repository<TimeSlot>().GetByConditionAsync(s => slots.Contains(s.Id)))
                .ToDictionary(s => s.Id, s => s.Label);
            return rows
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => ToView(r,
                    cMap.TryGetValue(r.CourtId, out var cn) ? cn : "",
                    sMap.TryGetValue(r.SlotId, out var sn) ? sn : ""))
                .ToList();
        }

        public async Task<(IReadOnlyList<WaitlistEntryView> Items, int Total)> GetMinePagedAsync(int userId, Page page)
        {
            var (rows, total) = await WRepo.GetPagedAsync(w => w.UserId == userId && w.DeletedAt == null, w => w.CreatedAt, OrderDirection.Desc, page);
            var courtIds = rows.Select(w => w.CourtId).Distinct().ToList(); var slotIds = rows.Select(w => w.SlotId).Distinct().ToList();
            var courts = (await Repository<Court>().GetByConditionAsync(c => courtIds.Contains(c.Id))).ToDictionary(c => c.Id, c => c.Name);
            var slots = (await Repository<TimeSlot>().GetByConditionAsync(s => slotIds.Contains(s.Id))).ToDictionary(s => s.Id, s => s.Label);
            return (rows.Select(w => ToView(w, courts.GetValueOrDefault(w.CourtId, "?"), slots.GetValueOrDefault(w.SlotId, "?"))).ToList(), total);
        }

        public async Task<int> ExpirePastEntriesAsync()
        {
            var now = Badminton.Core.Utils.HanoiTime.HanoiNow();
            var today = DateOnly.FromDateTime(now);
            var entries = await WRepo.GetByConditionAsync(w => w.Status == WaitlistStatus.Waiting && w.Date <= today);
            var slots = (await Repository<TimeSlot>().GetAllAsync()).ToDictionary(s => s.Id);
            int count = 0;
            foreach (var entry in entries.Where(w => !slots.TryGetValue(w.SlotId, out var slot)
                || Badminton.Core.Utils.HanoiTime.SlotStart(w.Date, slot.StartTime) < now.AddMinutes(30)))
                await Uow.RunInTransactionAsync(async () => {
                    await Uow.LockBookingRequestAsync($"waitlist-finish:{entry.Id}");
                    var current = (await WRepo.GetByConditionAsync(w => w.Id == entry.Id)).Single();
                    if (current.Status != WaitlistStatus.Waiting) return;
                    current.Status = WaitlistStatus.Expired;
                    current.DeclineReason = "Đã qua thời hạn nhận chỗ.";
                    current.ResolvedAt = DateTime.UtcNow;
                    WRepo.Update(current);
                    await SaveAsync(); count++;
                });
            return count;
        }

        public async Task<Booking?> PromoteAsync(int courtId, int slotId,
            DateOnly date, int? freeReason = null)
        {
            var candidates = await WRepo.GetByConditionAsync(w => w.CourtId == courtId && w.SlotId == slotId
                && w.Date == date && w.Status == WaitlistStatus.Waiting);
            foreach (var candidate in candidates.OrderBy(w => w.CreatedAt).ThenBy(w => w.Id))
            {
                Booking? result = null;
                try { await Uow.RunInTransactionAsync(async () => {
                    // Shared with leave/expiry; booking and promotion now commit together.
                    await Uow.LockBookingRequestAsync($"waitlist-finish:{candidate.Id}");
                    var winner = (await WRepo.GetByConditionAsync(w => w.Id == candidate.Id)).Single();
                    if (winner.Status != WaitlistStatus.Waiting) return;
                    var recovered = (await Repository<Booking>().GetByConditionAsync(b => b.IdempotencyKey == $"waitlist:{winner.Id}"
                        && b.UserId == winner.UserId && b.CourtId == courtId && b.SlotId == slotId && b.Date == date)).SingleOrDefault();
                    if (recovered is not null) {
                        if (recovered.Status is BookingStatus.Pending or BookingStatus.Confirmed) result = await FinishPromotionAsync(winner.Id, recovered.Id);
                        else { winner.Status = WaitlistStatus.Expired; winner.ResolvedAt = DateTime.UtcNow; WRepo.Update(winner); await SaveAsync(); }
                        return;
                    }
                    var user = await Repository<User>().GetByIdAsync(winner.UserId);
                    var month = Badminton.Core.Utils.HanoiTime.HanoiToday().MonthStart();
                    if (user is null || user.DeletedAt.HasValue || user.Status != UserStatus.Active || !user.IsVerified
                        || (user.NoShowLimited && user.NoShowLimitedPeriod == month))
                        return; // Preserve their place if eligibility is restored; do not block eligible followers.
                    var slot = await Repository<TimeSlot>().GetByIdAsync(slotId)
                        ?? throw new Badminton.Core.Base.NotFoundException("TimeSlot", slotId);
                    var active = await CountOverlappingBookingsAsync(courtId, date, slot);
                    if (active > 0) return;
                    var created = await _booking.CreateAsync(new CreateBookingRequest(courtId, date.ToString("yyyy-MM-dd"), slotId,
                        winner.PlayerCount, IdempotencyKey: $"waitlist:{winner.Id}", Note: winner.Note), winner.UserId);
                    result = await FinishPromotionAsync(winner.Id, created.Id);
                }); }
                catch (Badminton.Core.Base.ConflictException) { continue; } // Entire attempt rolled back before examining another waiter.
                catch (Exception ex) when (ex is Badminton.Core.Base.BadRequestException or Badminton.Core.Base.NotFoundException) {
                    await Uow.RunInTransactionAsync(async () => {
                        await Uow.LockBookingRequestAsync($"waitlist-finish:{candidate.Id}");
                        var row = (await WRepo.GetByConditionAsync(w => w.Id == candidate.Id)).Single();
                        if (row.Status != WaitlistStatus.Waiting) return;
                        row.Status = WaitlistStatus.Expired; row.DeclineReason = ex.Message; row.ResolvedAt = DateTime.UtcNow;
                        WRepo.Update(row); await SaveAsync();
                    });
                }
                if (result is not null) { await _notify.NotifyWaitlistPromotedAsync(candidate.Id, result.Id); return result; }
            }
            return null;
        }

        // Same strict interval intersection as availability/create: adjacent slots are free.
        private Task<int> CountOverlappingBookingsAsync(int courtId, DateOnly date, TimeSlot slot) =>
            Repository<Booking>().CountByConditionAsync(b => b.CourtId == courtId && b.Date == date
                && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)
                && b.Slot != null && b.Slot.StartTime < slot.EndTime && slot.StartTime < b.Slot.EndTime);

        private async Task<Booking?> FinishPromotionAsync(int entryId, int bookingId)
        {
            await Uow.RunInTransactionAsync(async () => {
                await Uow.LockBookingRequestAsync($"waitlist-finish:{entryId}");
                var winner = (await WRepo.GetByConditionAsync(w => w.Id == entryId)).Single();
                if (winner.Status != WaitlistStatus.Waiting) return;
                winner.Status = WaitlistStatus.Promoted;
                winner.PromotedBookingId = bookingId;
                winner.ResolvedAt = DateTime.UtcNow;
                WRepo.Update(winner);
                await _notify.QueueWaitlistPromotedAsync(winner.Id, bookingId);
                await SaveAsync();
            });
            return await Repository<Booking>().GetByIdAsync(bookingId);
        }
    }
}
