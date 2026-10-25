using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.TimeSlot;
using Badminton.Core.Base;
using Badminton.Core.Security;
using ModelViews.TimeSlot;

namespace Services
{
    public sealed class TimeSlotService : BaseService, ITimeSlotService
    {
        private readonly IAuditService _audit;
        public TimeSlotService(IUnitOfWork uow, ICurrentUser currentUser, IAuditService audit)
            : base(uow, currentUser) { _audit = audit; }

        private IGenericRepository<TimeSlot> Repo => Repository<TimeSlot>();

        public async Task<IReadOnlyList<TimeSlotView>> GetAllAsync(bool includeInactive = false)
        {
            var slots = await Repo.GetByConditionAsync(s => (includeInactive || s.IsActive) && s.DeletedAt == null);
            return slots.OrderBy(s => s.StartTime).Select(ToView).ToList();
        }

        public async Task<int> CreateAsync(CreateTimeSlotRequest request, int actorId)
        {
            int result = 0;
            await Uow.RunInTransactionAsync(async () =>
            {
                await LockInventoryAsync();
                result = await CreateCoreAsync(request, actorId);
                await new InventoryHistoryService(Uow).CaptureTodayAsync();
            });
            return result;
        }

        private async Task<int> CreateCoreAsync(CreateTimeSlotRequest request, int actorId)
        {
            var start = ParseTime(request.StartTime);
            var end = ParseTime(request.EndTime);
            if (end - start != TimeSpan.FromHours(1))
                throw new BadRequestException("Khung giờ phải dài đúng 60 phút.");

            var dupes = await Repo.GetByConditionAsync(
                s => s.StartTime < end && start < s.EndTime && s.IsActive && s.DeletedAt == null);
            if (dupes.Count > 0)
                throw new ConflictException("Khung giờ giao với khung giờ đang hoạt động.");

            var s = new TimeSlot
            {
                Label = request.Label,
                StartTime = start,
                EndTime = end,
                IsPeak = request.IsPeak || start.Hour >= 18
            };
            await Repo.AddAsync(s);
            await SaveAsync();
            await _audit.LogAsync(actorId, "timeslot.create", "TimeSlot", s.Id.ToString(), null, s);
            return s.Id;
        }

        public Task UpdateAsync(int id, UpdateTimeSlotRequest request, int actorId) => Uow.RunInTransactionAsync(async () =>
        {
            await LockInventoryAsync();
            await UpdateCoreAsync(id, request, actorId);
            await new InventoryHistoryService(Uow).CaptureTodayAsync();
        });

        private async Task UpdateCoreAsync(int id, UpdateTimeSlotRequest request, int actorId)
        {
            var s = await MustGetAsync(id);
            var before = new { s.Label, s.IsActive };
            if (request.Label is not null) s.Label = request.Label;
            if (request.IsActive == true && !s.IsActive)
            {
                var overlaps = await Repo.CountByConditionAsync(x => x.Id != id && x.IsActive && x.DeletedAt == null
                    && x.StartTime < s.EndTime && s.StartTime < x.EndTime);
                if (overlaps > 0) throw new ConflictException("Khung giờ giao với khung giờ đang hoạt động.");
            }
            if (request.IsActive.HasValue) s.IsActive = request.IsActive.Value;
            s.UpdatedAt = DateTime.UtcNow;
            Repo.Update(s);
            await SaveAsync();
            await _audit.LogAsync(actorId, "timeslot.update", "TimeSlot", id.ToString(), before, s);
        }

        public Task DeleteAsync(int id, int actorId) => Uow.RunInTransactionAsync(async () =>
        {
            await LockInventoryAsync();
            await DeleteCoreAsync(id, actorId);
            await new InventoryHistoryService(Uow).CaptureTodayAsync();
        });

        private async Task DeleteCoreAsync(int id, int actorId)
        {
            var s = await MustGetAsync(id);
            var before = new { s.Label, s.IsActive };
            s.IsActive = false;
            s.DeletedAt = DateTime.UtcNow;
            s.UpdatedAt = DateTime.UtcNow;
            Repo.Update(s);
            await SaveAsync();
            await _audit.LogAsync(actorId, "timeslot.delete", "TimeSlot", id.ToString(), before, s);
        }

        private async Task LockInventoryAsync()
        {
            var branches = await Repository<Branch>().GetAllAsync();
            foreach (var branch in branches.OrderBy(b => b.Id)) await Uow.LockBookingRequestAsync($"branch-config:{branch.Id}");
            await Uow.LockTimeSlotsAsync();
        }

        private async Task<TimeSlot> MustGetAsync(int id)
        {
            var s = await Repo.GetByIdAsync(id);
            if (s is null || s.DeletedAt.HasValue) throw new NotFoundException("TimeSlot", id);
            return s;
        }

        private static TimeOnly ParseTime(string s) =>
            TimeOnly.TryParse(s, out var t) ? t : throw new BadRequestException("Giờ không hợp lệ.");

        private static TimeSlotView ToView(TimeSlot s) => new(
            s.Id, s.Label, s.StartTime.ToString("HH\\:mm"), s.EndTime.ToString("HH\\:mm"),
            s.IsPeak, s.IsActive);
    }
}
