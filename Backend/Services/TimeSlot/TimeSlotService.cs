using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.TimeSlot;
using Badminton.Core.Base;
using Microsoft.AspNetCore.Http;
using ModelViews.TimeSlot;

namespace Services
{
    public sealed class TimeSlotService : BaseService, ITimeSlotService
    {
        private readonly IAuditService _audit;
        public TimeSlotService(IUnitOfWork uow, IHttpContextAccessor http, IAuditService audit)
            : base(uow, http) { _audit = audit; }

        private IGenericRepository<TimeSlot> Repo => Repository<TimeSlot>();

        public async Task<IReadOnlyList<TimeSlotView>> GetAllAsync()
        {
            var slots = await Repo.GetByConditionAsync(s => s.IsActive && s.DeletedAt == null);
            return slots.OrderBy(s => s.StartTime).Select(ToView).ToList();
        }

        public async Task<int> CreateAsync(CreateTimeSlotRequest request, int actorId)
        {
            var start = ParseTime(request.StartTime);
            var end = ParseTime(request.EndTime);
            if (end <= start)
                throw new BadRequestException("Giờ kết thúc phải sau giờ bắt đầu.");

            var dupes = await Repo.GetByConditionAsync(
                s => s.StartTime == start && s.EndTime == end && s.DeletedAt == null);
            if (dupes.Count > 0)
                throw new ConflictException("Khung giờ này đã tồn tại.");

            var s = new TimeSlot
            {
                Label = request.Label,
                StartTime = start,
                EndTime = end,
                IsPeak = request.IsPeak || start.Hour >= 18
            };
            _ = Repo.AddAsync(s);
            await SaveAsync();
            await _audit.LogAsync(actorId, "timeslot.create", "TimeSlot", s.Id.ToString(), null, s);
            return s.Id;
        }

        public async Task UpdateAsync(int id, UpdateTimeSlotRequest request, int actorId)
        {
            var s = await MustGetAsync(id);
            var before = new { s.Label, s.IsActive };
            if (request.Label is not null) s.Label = request.Label;
            if (request.IsActive.HasValue) s.IsActive = request.IsActive.Value;
            s.UpdatedAt = DateTime.UtcNow;
            Repo.Update(s);
            await SaveAsync();
            await _audit.LogAsync(actorId, "timeslot.update", "TimeSlot", id.ToString(), before, s);
        }

        public async Task DeleteAsync(int id, int actorId)
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

        private async Task<TimeSlot> MustGetAsync(int id)
        {
            var s = await Repo.GetByIdAsync(id);
            if (s is null || s.DeletedAt.HasValue) throw new NotFoundException("TimeSlot", id);
            return s;
        }

        private static TimeOnly ParseTime(string s) =>
            TimeOnly.TryParse(s, out var t) ? t : throw new BadRequestException("Giờ không hợp lệ.");

        private static TimeSlotView ToView(TimeSlot s) => new(
            s.Id, s.Label, s.StartTime.ToString("hh\\:mm"), s.EndTime.ToString("hh\\:mm"),
            s.IsPeak, s.IsActive);
    }
}
