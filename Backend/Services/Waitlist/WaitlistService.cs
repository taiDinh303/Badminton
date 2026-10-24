using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Booking;
using Contract.Services.Notify;
using Contract.Services.Waitlist;
using Badminton.Core.Enums;
using ModelViews.Booking;

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
                w.Status, w.CreatedAt, w.PromotedBookingId);

        public async Task<WaitlistEntryView> JoinAsync(int userId, int courtId,
            int slotId, DateOnly date, int playerCount, string? note)
        {
            var active = await Repository<Booking>()
                .CountByConditionAsync(b =>
                    b.CourtId == courtId && b.SlotId == slotId && b.Date == date
                    && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Pending));
            if (active == 0)
                throw new Badminton.Core.Base.BadRequestException(
                    "Slot còn trống, không cần danh sách chờ — hãy đặt trực tiếp.");

            var court = await Repository<Court>().GetByIdAsync(courtId);
            var slot = await Repository<TimeSlot>().GetByIdAsync(slotId);
            if (court is null || slot is null)
                throw new Badminton.Core.Base.NotFoundException("Court or Slot", courtId);

            if (playerCount < 1 || playerCount > court.Capacity) throw new Badminton.Core.Base.BadRequestException($"Số khách phải từ 1 đến {court.Capacity} (sức chứa sân).");
            if (!court.IsActive || !slot.IsActive) throw new Badminton.Core.Base.BadRequestException("Sân/khung giờ không hoạt động.");

            // The slot must still be in the future (Hanoi business clock).
            var startHanoi = Badminton.Core.Utils.HanoiTime.SlotStart(date, slot.StartTime);
            if (startHanoi <= Badminton.Core.Utils.HanoiTime.HanoiNow())
                throw new Badminton.Core.Base.BadRequestException(
                    "Slot này đã qua giờ bắt đầu, không thể chờ.");

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
            var e = await WRepo.GetByIdAsync(entryId);
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

        public async Task<Booking?> PromoteAsync(int courtId, int slotId,
            DateOnly date, int? freeReason = null)
        {
            var candidates = await WRepo.GetByConditionAsync(w =>
                w.CourtId == courtId && w.SlotId == slotId && w.Date == date
                && w.Status == WaitlistStatus.Waiting);
            if (candidates.Count == 0) return null;

            var winner = candidates.OrderBy(w => w.CreatedAt).First();
            // A crash may leave the booking committed before the waitlist row was resolved.
            var recovered = (await Repository<Booking>().GetByConditionAsync(b => b.IdempotencyKey == $"waitlist:{winner.Id}"
                && b.UserId == winner.UserId && b.CourtId == courtId && b.SlotId == slotId && b.Date == date)).SingleOrDefault();
            if (recovered is not null) return await FinishPromotionAsync(winner.Id, recovered.Id);
            var active = await Repository<Booking>()
                .CountByConditionAsync(b =>
                    b.CourtId == courtId && b.SlotId == slotId && b.Date == date
                    && (b.Status == BookingStatus.Confirmed
                        || b.Status == BookingStatus.Pending));
            if (active > 0) return null; // still occupied — not free



            var request = new CreateBookingRequest(
                CourtId: courtId,
                Date: date.ToString("yyyy-MM-dd"),
                SlotId: slotId,
                PlayerCount: winner.PlayerCount,
                GroupId: null,
                VoucherCode: null,
                IdempotencyKey: $"waitlist:{winner.Id}");

            BookingView created;
            try
            {
                created = await _booking.CreateAsync(request, winner.UserId);
            }
            catch (Badminton.Core.Base.ConflictException)
            {
                // A concurrent booking or temporary eligibility conflict must not discard the waiter.
                return null;
            }
            catch (Exception ex) when (ex is Badminton.Core.Base.BadRequestException or Badminton.Core.Base.NotFoundException)
            {
                winner.Status = WaitlistStatus.Expired;
                winner.DeclineReason = ex.Message;
                winner.ResolvedAt = DateTime.UtcNow;
                WRepo.Update(winner);
                await SaveAsync();
                return null;
            }

            return await FinishPromotionAsync(winner.Id, created.Id);
        }

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
            await _notify.NotifyWaitlistPromotedAsync(entryId, bookingId);
            return await Repository<Booking>().GetByIdAsync(bookingId);
        }
    }
}
