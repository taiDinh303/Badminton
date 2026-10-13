using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Court;
using M.Core.Base;
using Microsoft.AspNetCore.Http;
using ModelViews.Court;

namespace Services
{
    /// <summary>
    /// COURT service. Demonstrates batch-loading a related master dictionary
    /// (court types) to hydrate views in ONE round-trip instead of N+1.
    /// </summary>
    public sealed class CourtService : BaseService, ICourtService
    {
        private readonly IGenericRepository<CourtType> _courtTypes;

        public CourtService(IUnitOfWork uow, IHttpContextAccessor http) : base(uow, http)
        {
            _courtTypes = Repository<CourtType>();
        }

        public async Task<IReadOnlyList<CourtView>> GetAllAsync()
        {
            var courts = await Repo.GetByConditionAsync(
                x => x.IsActive && x.DeletedAt == null);
            var typeNames = await TypeNamesAsync();
            return courts.Select(c => new CourtView(
                c.Id, c.CourtCode, c.CourtName, c.Location,
                c.PricePerHour, c.Status,
                typeNames.TryGetValue(c.CourtTypeId, out var n) ? n : "Unknown")
                ).ToList();
        }

        public Task CreateAsync(CreateCourtRequest request, string userName)
        {
            var entity = new Court
            {
                CourtTypeId = request.CourtTypeId,
                CourtCode = request.CourtCode,
                CourtName = request.CourtName,
                Location = request.Location,
                PricePerHour = request.PricePerHour,
                ImageUrl = request.ImageUrl
            };
            ApplyCreateAudit(entity);
            _ = Repo.AddAsync(entity);
            return SaveAsync();
        }

        public async Task UpdateAsync(int id, UpdateCourtRequest request, string userName)
        {
            var e = await Repo.GetByIdAsync(id);
            if (e is null || e.DeletedAt.HasValue)
                throw new NotFoundException(nameof(Court), id);

            if (request.CourtName is not null) e.CourtName = request.CourtName;
            if (request.Location is not null) e.Location = request.Location;
            if (request.PricePerHour.HasValue) e.PricePerHour = request.PricePerHour.Value;
            if (request.Status.HasValue) e.Status = request.Status.Value;
            if (request.ImageUrl is not null) e.ImageUrl = request.ImageUrl;

            ApplyUpdateAudit(e);
            Repo.Update(e);
            await SaveAsync();
        }

        public async Task DeleteAsync(int id, string userName)
        {
            var e = await Repo.GetByIdAsync(id);
            if (e is null) throw new NotFoundException(nameof(Court), id);
            e.IsActive = false;
            ApplySoftDelete(e);
            Repo.Update(e);
            await SaveAsync();
        }

        // ---------- helpers ----------
        private IGenericRepository<Court> Repo => Repository<Court>();

        private async Task<Dictionary<int, string>> TypeNamesAsync()
        {
            var types = await _courtTypes.GetByConditionAsync(x => x.DeletedAt == null);
            return types.ToDictionary(t => t.Id, t => t.Name);
        }
    }
}
