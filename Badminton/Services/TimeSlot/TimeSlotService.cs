using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.TimeSlot;
using M.Core.Base;
using Microsoft.AspNetCore.Http;
using ModelViews.TimeSlot;

namespace Services
{
    /// <summary>
    /// TIME-SLOT service. Slots are global (applies to all courts); the
    /// unique StartTime+EndTime constraint lives in the DB.
    /// </summary>
    public sealed class TimeSlotService : BaseService, ITimeSlotService
    {
        public TimeSlotService(IUnitOfWork uow, IHttpContextAccessor http) : base(uow, http) { }

        private IGenericRepository<TimeSlot> Repo => Repository<TimeSlot>();

        public async Task<IReadOnlyList<TimeSlotView>> GetAllAsync()
        {
            var slots = await Repo.GetByConditionAsync(
                x => x.IsActive && x.DeletedAt == null);
            return slots.OrderBy(s => s.StartTime)
                .Select(s => new TimeSlotView(s.Id, s.StartTime, s.EndTime,
                    $"{s.StartTime:hh\\:mm}-{s.EndTime:hh\\:mm}"))
                .ToList();
        }

        public async Task CreateAsync(CreateTimeSlotRequest request, string userName)
        {
            if (request.EndTime <= request.StartTime)
                throw new BadRequestException("End time must be after start time.");

            var dupes = await Repo.GetByConditionAsync(
                x => x.StartTime == request.StartTime &&
                     x.EndTime == request.EndTime && x.DeletedAt == null);
            if (dupes.Count > 0)
                throw new ConflictException("This time slot already exists.");

            var entity = new TimeSlot
            {
                StartTime = request.StartTime,
                EndTime = request.EndTime
            };
            ApplyCreateAudit(entity);
            _ = Repo.AddAsync(entity);
            await SaveAsync();
        }

        public async Task UpdateAsync(int id, UpdateTimeSlotRequest request, string userName)
        {
            var e = await Repo.GetByIdAsync(id);
            if (e is null || e.DeletedAt.HasValue)
                throw new NotFoundException(nameof(TimeSlot), id);

            if (request.StartTime.HasValue) e.StartTime = request.StartTime.Value;
            if (request.EndTime.HasValue) e.EndTime = request.EndTime.Value;
            if (e.EndTime <= e.StartTime)
                throw new BadRequestException("End time must be after start time.");
            if (request.IsActive.HasValue) e.IsActive = request.IsActive.Value;

            ApplyUpdateAudit(e);
            Repo.Update(e);
            await SaveAsync();
        }

        public async Task DeleteAsync(int id, string userName)
        {
            var e = await Repo.GetByIdAsync(id);
            if (e is null) throw new NotFoundException(nameof(TimeSlot), id);
            e.IsActive = false;
            ApplySoftDelete(e);
            Repo.Update(e);
            await SaveAsync();
        }
    }
}
