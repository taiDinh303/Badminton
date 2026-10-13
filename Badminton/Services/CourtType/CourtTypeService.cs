using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.CourtType;
using M.Core.Base;
using Microsoft.AspNetCore.Http;
using ModelViews.CourtType;

namespace Services
{
    /// <summary>
    /// COURT-TYPE master service. Concrete subtype of the shared generic
    /// <see cref="NamedMasterCrudService{T}"/> (POLYMORPHISM: the base handles
    /// CRUD + audit; this class only maps the request DTO and the view).
    /// </summary>
    public sealed class CourtTypeService :
        NamedMasterCrudService<CourtType>, ICourtTypeService
    {
        public CourtTypeService(IUnitOfWork uow, IHttpContextAccessor http)
            : base(uow, http) { }

        public async Task<IReadOnlyList<CourtTypeView>> GetAllAsync()
        {
            var items = await GetAllActiveAsync();
            return items.Select(x => new CourtTypeView(
                x.Id, x.Name, x.Description, x.IsActive)).ToList();
        }

        public async Task<int> CreateAsync(CreateCourtTypeRequest request, string userName)
        {
            var dupes = await Repo.GetByConditionAsync(
                x => x.Name == request.Name && x.DeletedAt == null);
            if (dupes.Count > 0)
                throw new ConflictException($"Court type '{request.Name}' already exists.");

            var entity = new CourtType { Name = request.Name, Description = request.Description };
            await CreateWithAuditAsync(entity);
            return entity.Id;
        }

        public async Task UpdateAsync(int id, UpdateCourtTypeRequest request, string userName)
        {
            var e = await MustGetAsync(id);
            if (request.Name is not null) e.Name = request.Name;
            if (request.Description is not null) e.Description = request.Description;
            if (request.IsActive.HasValue) e.IsActive = request.IsActive.Value;
            ApplyUpdateAudit(e);
            Repo.Update(e);
            await SaveAsync();
        }

        public Task DeleteAsync(int id, string userName) => SoftDeleteByIdAsync(id);
    }
}
