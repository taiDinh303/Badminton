using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Court;
using Badminton.Core.Base;
using Badminton.Core.Enums;
using Microsoft.AspNetCore.Http;
using ModelViews.Court;

namespace Services
{
    public sealed class CourtService : BaseService, ICourtService
    {
        private readonly IAuditService _audit;
        public CourtService(IUnitOfWork uow, IHttpContextAccessor http, IAuditService audit)
            : base(uow, http) { _audit = audit; }

        private IGenericRepository<Court> Repo => Repository<Court>();

        public async Task<IReadOnlyList<CourtView>> GetAllAsync(int? branchId = null)
        {
            var courts = branchId is { } bid
                ? await Repo.GetByConditionAsync(c => c.BranchId == bid && c.IsActive && c.DeletedAt == null)
                : await Repo.GetByConditionAsync(c => c.IsActive && c.DeletedAt == null);
            return courts.Select(ToView).ToList();
        }

        public async Task<CourtView> GetByIdAsync(int id)
        {
            var c = await MustGetAsync(id);
            return ToView(c);
        }

        public async Task<int> CreateAsync(CreateCourtRequest request, int actorId)
        {
            if (request.PricePeak <= 0 || request.PriceLowPeak < 0)
                throw new BadRequestException("Giá peak phải dương.");
            var dupes = await Repo.GetByConditionAsync(
                c => c.BranchId == request.BranchId && c.Code == request.Code && c.DeletedAt == null);
            if (dupes.Count > 0)
                throw new ConflictException($"Sân '{request.Code}' đã tồn tại ở cơ sở {request.BranchId}.");

            var c = new Court
            {
                BranchId = request.BranchId,
                Code = request.Code,
                Name = request.Name,
                PricePeak = request.PricePeak,
                PriceLowPeak = request.PriceLowPeak,
                WeekendSurcharge = request.WeekendSurcharge,
                Capacity = request.Capacity,
                Surface = ParseSurface(request.Surface),
                Amenities = request.Amenities
            };
            _ = Repo.AddAsync(c);
            await SaveAsync();
            await _audit.LogAsync(actorId, "court.create", "Court", c.Id.ToString(), null, c);
            return c.Id;
        }

        public async Task UpdateAsync(int id, UpdateCourtRequest request, int actorId)
        {
            var c = await MustGetAsync(id);
            var before = AuditCopy(c);
            if (request.Name is not null) c.Name = request.Name;
            if (request.PriceLowPeak.HasValue) c.PriceLowPeak = request.PriceLowPeak.Value;
            if (request.PricePeak.HasValue) c.PricePeak = request.PricePeak.Value;
            if (request.WeekendSurcharge.HasValue) c.WeekendSurcharge = request.WeekendSurcharge.Value;
            if (request.Capacity.HasValue) c.Capacity = request.Capacity.Value;
            if (request.Surface is not null) c.Surface = ParseSurface(request.Surface);
            if (request.Amenities is not null) c.Amenities = request.Amenities;
            if (request.IsActive.HasValue) c.IsActive = request.IsActive.Value;
            c.UpdatedAt = DateTime.UtcNow;
            Repo.Update(c);
            await SaveAsync();
            await _audit.LogAsync(actorId, "court.update", "Court", id.ToString(), before, c,
                "Chỉnh giá/cấu hình sân (phi-phiên bản, giữ snapshot booking đã xác nhận)");
        }

        public async Task DeleteAsync(int id, int actorId)
        {
            var c = await MustGetAsync(id);
            var before = AuditCopy(c);
            c.IsActive = false;
            c.DeletedAt = DateTime.UtcNow;
            c.UpdatedAt = DateTime.UtcNow;
            Repo.Update(c);
            await SaveAsync();
            await _audit.LogAsync(actorId, "court.delete", "Court", id.ToString(), before, c);
        }

        private async Task<Court> MustGetAsync(int id)
        {
            var c = await Repo.GetByIdAsync(id);
            if (c is null || c.DeletedAt.HasValue) throw new NotFoundException("Court", id);
            return c;
        }

        private static CourtSurface ParseSurface(string s) =>
            Enum.TryParse<CourtSurface>(s, ignoreCase: true, out var v) ? v : CourtSurface.Sindalo;

        private static CourtView ToView(Court c) => new(
            c.Id, c.BranchId, c.Code, c.Name, c.PriceLowPeak, c.PricePeak,
            c.WeekendSurcharge, c.Capacity, c.Surface.ToString(), c.IsActive,
            c.RatingAvg, c.RatingCount);

        private static object? AuditCopy(Court c) => new
        {
            c.Name, c.PriceLowPeak, c.PricePeak, c.WeekendSurcharge, c.Capacity,
            c.Surface, c.Amenities, c.IsActive
        };
    }
}
