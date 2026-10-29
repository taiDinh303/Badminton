using Badminton.Core.Base;
using Badminton.Core.Time;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Operations;
using ModelViews.Operations;
namespace Services.Operations;

public sealed class CoachingService(IUnitOfWork uow, IOperationsAccess access, IBusinessClock clock, ICoachCourtService courts, IBookingTransitionGuard transitions) : ICoachingService
{
    public async Task<IReadOnlyList<CoachView>> CoachesAsync(int branchId)
    {
        var actor = await access.ActorAsync();
        var staff = actor.Role == Badminton.Core.Enums.Role.Admin || (actor.Role == Badminton.Core.Enums.Role.Manager && actor.BranchId == branchId);
        return (await uow.GetRepository<CoachProfile>().GetByConditionAsync(x => x.BranchId == branchId && (x.Active || staff))).Select(x => new CoachView(x.Id, x.BranchId, x.Name, x.Bio, x.Active)).ToArray();
    }
    public async Task<int> SaveCoachAsync(int? id, CoachRequest r)
    {
        await access.StaffBranchAsync(r.BranchId);
        var branch = await uow.GetRepository<Branch>().GetByIdAsync(r.BranchId);
        if (branch is null || branch.DeletedAt.HasValue || branch.Status != Badminton.Core.Enums.BranchStatus.Active)
            throw new BadRequestException("Cơ sở không hoạt động.");
        var row = new CoachProfile { BranchId = r.BranchId };
        await uow.RunInTransactionAsync(async () =>
        {
            if (id.HasValue)
            {
                await uow.LockNotificationAsync($"coach:{id}");
                row = await Coach(id.Value);
                if (row.BranchId != r.BranchId)
                    throw new ConflictException("Không đổi cơ sở HLV.");
            }
            row.Name = ClubInput.Text(r.Name, 120, "Tên HLV");
            row.Bio = ClubInput.Text(r.Bio, 1000, "Giới thiệu");
            row.Active = r.Active;
            row.UpdatedAt = clock.UtcNow;
            if (id.HasValue)
                uow.GetRepository<CoachProfile>().Update(row);
            else
                await uow.GetRepository<CoachProfile>().AddAsync(row);
            await uow.SaveChangesAsync();
        });
        return row.Id;
    }
    public async Task<IReadOnlyList<CoachSlotView>> SlotsAsync(int coachId, DateTime fromUtc, DateTime toUtc)
    {
        await access.ActorAsync();
        ClubInput.Range(fromUtc, toUtc, TimeSpan.FromDays(31));
        var coach = await Coach(coachId);
        var rows = await uow.GetRepository<CoachSlot>().GetByConditionAsync(x => x.CoachId == coachId && x.StartsAtUtc >= fromUtc && x.StartsAtUtc < toUtc);
        var ids = rows.Select(x => x.Id).ToArray();
        var reservations = await uow.GetRepository<CoachingReservation>().GetByConditionAsync(x => ids.Contains(x.SlotId) && (x.Status == "Reserved" || x.Status == "Completed"));
        var result = new List<CoachSlotView>();
        foreach (var x in rows.OrderBy(x => x.StartsAtUtc))
        {
            var ready = await courts.ReadyAsync(x.CourtBookingId, x.StartsAtUtc, x.EndsAtUtc, x.Capacity, coach.BranchId);
            result.Add(new(x.Id, x.CoachId, x.StartsAtUtc, x.EndsAtUtc, x.Capacity, x.Cancelled || !ready ? 0 : x.Capacity - reservations.Where(r => r.SlotId == x.Id).Sum(r => r.People), x.PricePerPerson, x.Cancelled, x.CourtBookingId, ready));
        }
        return result;
    }
    public async Task<int> AddSlotAsync(int coachId, CoachSlotRequest r)
    {
        ClubInput.Range(r.StartsAtUtc, r.EndsAtUtc, TimeSpan.FromHours(8));
        ClubInput.Number(r.Capacity, 1, 20, "Số chỗ");
        ClubInput.Number(r.PricePerPerson, 0, 10000000, "Học phí mỗi người");
        if (r.StartsAtUtc <= clock.UtcNow || r.StartsAtUtc > clock.UtcNow.AddDays(90))
            throw new BadRequestException("Chọn lịch trong 90 ngày tới.");
        var slot = new CoachSlot { CoachId = coachId, StartsAtUtc = r.StartsAtUtc, EndsAtUtc = r.EndsAtUtc, Capacity = r.Capacity, PricePerPerson = r.PricePerPerson };
        await uow.RunInTransactionAsync(async () =>
        {
            if (r.CourtBookingId is { } bookingId)
                await transitions.AcquireAsync(bookingId);
            await uow.LockNotificationAsync($"coach:{coachId}");
            var coach = await Coach(coachId);
            await access.StaffBranchAsync(coach.BranchId);
            if (!coach.Active)
                throw new ConflictException("HLV đã ngừng hoạt động.");
            if (await uow.GetRepository<CoachSlot>().CountByConditionAsync(x => x.CoachId == coachId && !x.Cancelled && x.StartsAtUtc < r.EndsAtUtc && x.EndsAtUtc > r.StartsAtUtc) > 0)
                throw new ConflictException("HLV đã có lịch trong khoảng giờ này.");
            await uow.GetRepository<CoachSlot>().AddAsync(slot);
            await uow.SaveChangesAsync();
            if (r.CourtBookingId is { } bookedCourt)
                await courts.AttachAsync(slot.Id, bookedCourt);
        });
        return slot.Id;
    }
    public Task CancelSlotAsync(int id) => uow.RunInTransactionAsync(async () =>
    {
        var slot = await Slot(id);
        await uow.LockNotificationAsync($"coach:{slot.CoachId}");
        slot = await uow.GetRepository<CoachSlot>().GetFreshByIdAsync(id) ?? throw new NotFoundException("Lịch HLV", id);
        var coach = await Coach(slot.CoachId);
        await access.StaffBranchAsync(coach.BranchId);
        if (slot.Cancelled)
            return;
        if (slot.StartsAtUtc <= clock.UtcNow)
            throw new ConflictException("Không hủy buổi học đã bắt đầu.");
        slot.Cancelled = true;
        slot.UpdatedAt = clock.UtcNow;
        uow.GetRepository<CoachSlot>().Update(slot);
        var repo = uow.GetRepository<CoachingReservation>();
        foreach (var candidate in await repo.GetByConditionAsync(x => x.SlotId == id && x.Status == "Reserved"))
        {
            var row = (await repo.GetFreshByIdAsync(candidate.Id))!;
            row.Status = "Cancelled";
            row.UpdatedAt = clock.UtcNow;
            repo.Update(row);
        }
        await uow.SaveChangesAsync();
    });
    public async Task<int> ReserveAsync(CoachingRequest r)
    {
        await access.ActorAsync();
        ClubInput.Number(r.People, 1, 20, "Số người");
        var key = ClubInput.Text(r.RequestKey, 60, "Mã yêu cầu");
        var result = 0;
        await uow.RunInTransactionAsync(async () =>
        {
            await uow.LockNotificationAsync($"coaching-request:{access.ActorId}:{key}");
            var repo = uow.GetRepository<CoachingReservation>();
            var old = (await repo.GetByConditionAsync(x => x.UserId == access.ActorId && x.RequestKey == key)).SingleOrDefault();
            if (old is not null)
            {
                if (old.SlotId != r.SlotId || old.People != r.People)
                    throw new ConflictException("Mã yêu cầu đã dùng cho nội dung khác.");
                result = old.Id;
                return;
            }
            var slot = await Slot(r.SlotId);
            var courtBooking = slot.CourtBookingId;
            if (courtBooking is { } bookingId)
                await transitions.AcquireAsync(bookingId);
            await uow.LockNotificationAsync($"coach:{slot.CoachId}");
            slot = await Slot(r.SlotId);
            var coach = await Coach(slot.CoachId);
            if (slot.CourtBookingId != courtBooking || !await courts.ReadyAsync(slot.CourtBookingId, slot.StartsAtUtc, slot.EndsAtUtc, slot.Capacity, coach.BranchId))
                throw new ConflictException("Lớp chưa có sân hợp lệ hoặc lịch sân đã thay đổi. Vui lòng chọn lớp khác.");
            var branch = await uow.GetRepository<Branch>().GetByIdAsync(coach.BranchId);
            if (!coach.Active || slot.Cancelled || slot.StartsAtUtc <= clock.UtcNow || branch is null || branch.DeletedAt.HasValue || branch.Status != Badminton.Core.Enums.BranchStatus.Active)
                throw new ConflictException("Buổi học không còn nhận đặt chỗ.");
            var used = (await repo.GetByConditionAsync(x => x.SlotId == r.SlotId && (x.Status == "Reserved" || x.Status == "Completed"))).Sum(x => x.People);
            if (used + r.People > slot.Capacity)
                throw new ConflictException("Buổi học không đủ chỗ.");
            if (await repo.CountByConditionAsync(x => x.UserId == access.ActorId && x.SlotId == r.SlotId && x.Status == "Reserved") > 0)
                throw new ConflictException("Bạn đã đặt buổi này; hủy trước khi đặt lại.");
            var row = new CoachingReservation { SlotId = slot.Id, UserId = access.ActorId, People = r.People, Total = checked(r.People * slot.PricePerPerson), RequestKey = key };
            await repo.AddAsync(row);
            await uow.SaveChangesAsync();
            result = row.Id;
        });
        return result;
    }
    public async Task<IReadOnlyList<CoachingView>> ReservationsAsync(bool staff, int? branchId, int page)
    {
        await access.ActorAsync();
        var branch = staff ? await access.StaffBranchAsync(branchId) : null;
        var coaches = (await uow.GetRepository<CoachProfile>().GetByConditionAsync(x => !branch.HasValue || x.BranchId == branch)).ToDictionary(x => x.Id);
        var coachIds = coaches.Keys.ToArray();
        var slots = (await uow.GetRepository<CoachSlot>().GetByConditionAsync(x => coachIds.Contains(x.CoachId))).ToDictionary(x => x.Id);
        var ids = slots.Keys.ToArray();
        var (rows, _) = await uow.GetRepository<CoachingReservation>().GetPagedAsync(x => ids.Contains(x.SlotId) && (staff || x.UserId == access.ActorId), x => x.CreatedAt, OrderDirection.Desc, new(page, 30));
        return rows.Select(x => { var slot = slots[x.SlotId]; var coach = coaches[slot.CoachId]; return new CoachingView(x.Id, x.UserId, coach.BranchId, coach.Name, slot.StartsAtUtc, slot.EndsAtUtc, x.People, x.Total, x.Status); }).ToArray();
    }
    public Task TransitionAsync(int id, string action) => uow.RunInTransactionAsync(async () =>
    {
        await access.ActorAsync();
        var repo = uow.GetRepository<CoachingReservation>();
        var row = (await repo.GetByConditionAsync(x => x.Id == id)).SingleOrDefault() ?? throw new NotFoundException("Đặt HLV", id);
        var slot = await Slot(row.SlotId);
        await uow.LockNotificationAsync($"coach:{slot.CoachId}");
        row = await repo.GetFreshByIdAsync(id) ?? throw new NotFoundException("Đặt HLV", id);
        slot = await Slot(row.SlotId);
        var coach = await Coach(slot.CoachId);
        if (action == "cancel")
        {
            if (row.UserId != access.ActorId)
                await access.StaffBranchAsync(coach.BranchId);
            if (row.Status == "Cancelled")
                return;
            if (slot.StartsAtUtc <= clock.UtcNow || row.Status != "Reserved")
                throw new ConflictException("Chỉ hủy trước khi bắt đầu.");
            row.Status = "Cancelled";
        }
        else if (action == "complete")
        {
            await access.StaffBranchAsync(coach.BranchId);
            if (row.Status == "Completed")
                return;
            if (row.Status != "Reserved" || slot.EndsAtUtc > clock.UtcNow)
                throw new ConflictException("Chỉ hoàn thành sau giờ kết thúc.");
            row.Status = "Completed";
        }
        else
            throw new BadRequestException("Thao tác không hợp lệ.");
        row.UpdatedAt = clock.UtcNow;
        repo.Update(row);
        await uow.SaveChangesAsync();
    });
    private async Task<CoachProfile> Coach(int id) => await uow.GetRepository<CoachProfile>().GetFreshByIdAsync(id) ?? throw new NotFoundException("HLV", id);
    public Task AttachCourtAsync(int id, int bookingId) => courts.AttachAsync(id, bookingId);
    public Task<IReadOnlyList<CoachCourtChoice>> CourtChoicesAsync(int branchId) => courts.ChoicesAsync(branchId);
    private async Task<CoachSlot> Slot(int id) => (await uow.GetRepository<CoachSlot>().GetByConditionAsync(x => x.Id == id)).SingleOrDefault() ?? throw new NotFoundException("Lịch HLV", id);
}
