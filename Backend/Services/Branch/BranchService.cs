using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Branch;
using Badminton.Core.Base;
using Badminton.Core.Security;
using ModelViews.Branch;

namespace Services
{
    public sealed class BranchService : BaseService, IBranchService
    {
        private readonly IAuditService _audit;
        public BranchService(IUnitOfWork uow, ICurrentUser currentUser, IAuditService audit)
            : base(uow, currentUser) { _audit = audit; }

        private IGenericRepository<Branch> Repo => Repository<Branch>();

        public async Task<IReadOnlyList<BranchView>> GetAllAsync()
        {
            var branches = await Repo.GetByConditionAsync(b => b.DeletedAt == null);

            var activeCourts = await Repository<Court>()
                .GetByConditionAsync(c => c.IsActive && c.DeletedAt == null);
            var counts = activeCourts
                .GroupBy(c => c.BranchId)
                .ToDictionary(g => g.Key, g => g.Count());

            return branches
                .Select(b => new BranchView(
                    b.Id, b.Name, b.Address, b.IsActive,
                    b.OpenTime.ToString("hh\\:mm"), b.CloseTime.ToString("hh\\:mm"),
                    counts.TryGetValue(b.Id, out var n) ? n : 0))
                .ToList();
        }

        public async Task<BranchView> GetByIdAsync(int id)
        {
            var b = await MustGetAsync(id);
            var courtCount = await Repository<Court>()
                .CountByConditionAsync(c => c.BranchId == id && c.IsActive && c.DeletedAt == null);
            return new BranchView(b.Id, b.Name, b.Address, b.IsActive,
                b.OpenTime.ToString("hh\\:mm"), b.CloseTime.ToString("hh\\:mm"), courtCount);
        }

        public async Task<int> CreateAsync(CreateBranchRequest request, int actorId)
        {
            var dupes = await Repo.GetByConditionAsync(b => b.Name == request.Name && b.DeletedAt == null);
            if (dupes.Count > 0)
                throw new ConflictException($"Cơ sở '{request.Name}' đã tồn tại.");

            var b = new Branch
            {
                Name = request.Name,
                Address = request.Address,
                GeoLat = request.GeoLat,
                GeoLng = request.GeoLng,
                OpenTime = ParseTime(request.OpenTime, new TimeOnly(6, 0)),
                CloseTime = ParseTime(request.CloseTime, new TimeOnly(23, 0))
            };
            _ = Repo.AddAsync(b);
            await SaveAsync();
            await _audit.LogAsync(actorId, "branch.create", "Branch", b.Id.ToString(), null, b);
            return b.Id;
        }

        public async Task UpdateAsync(int id, UpdateBranchRequest request, int actorId)
        {
            var b = await MustGetAsync(id);
            var before = AuditCopy(b);
            if (request.Name is not null) b.Name = request.Name;
            if (request.Address is not null) b.Address = request.Address;
            if (request.IsActive.HasValue) b.IsActive = request.IsActive.Value;
            if (request.OpenTime is not null) b.OpenTime = ParseTime(request.OpenTime, b.OpenTime);
            if (request.CloseTime is not null) b.CloseTime = ParseTime(request.CloseTime, b.CloseTime);
            b.UpdatedAt = DateTime.UtcNow;
            Repo.Update(b);
            await SaveAsync();
            await _audit.LogAsync(actorId, "branch.update", "Branch", id.ToString(), before, b);
        }

        public async Task DeleteAsync(int id, int actorId)
        {
            var b = await MustGetAsync(id);
            var before = AuditCopy(b);
            b.IsActive = false;
            b.DeletedAt = DateTime.UtcNow;
            b.UpdatedAt = DateTime.UtcNow;
            Repo.Update(b);
            await SaveAsync();
            await _audit.LogAsync(actorId, "branch.delete", "Branch", id.ToString(), before, b);
        }

        private async Task<Branch> MustGetAsync(int id)
        {
            var b = await Repo.GetByIdAsync(id);
            if (b is null || b.DeletedAt.HasValue)
                throw new NotFoundException("Branch", id);
            return b;
        }

        private static TimeOnly ParseTime(string s, TimeOnly fallback) =>
            TimeOnly.TryParse(s, out var t) ? t : fallback;

        private static object? AuditCopy(Branch b) => new
        {
            b.Name, b.Address, b.IsActive, b.OpenTime, b.CloseTime
        };
    }
}
