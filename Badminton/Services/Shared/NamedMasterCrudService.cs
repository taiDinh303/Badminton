using Contract.Repositories.UnitOfWork;
using M.Core.Base;
using Microsoft.AspNetCore.Http;

namespace Services
{
    /// <summary>
    /// POLYMORPHISM (đa hình) + INHERITANCE (kế thừa): ONE generic CRUD
    /// implementation for every named-master entity. <c>CourtTypeService</c>
    /// subclasses it; any future master table does the same. The base owns
    /// the "fetch-or-throw", active-listing, and audit/soft-delete rules
    /// against a concrete <c>T</c>; subclasses only supply the concrete type
    /// and their request-mapping. One base -> many identical-behaviour
    /// implementations (Open/Closed).
    /// </summary>
    public abstract class NamedMasterCrudService<T> : BaseService
        where T : NamedIntMasterEntity, new()
    {
        protected NamedMasterCrudService(IUnitOfWork uow, IHttpContextAccessor http)
            : base(uow, http) { }

        /// <summary>Encapsulation: the concrete repository for <c>T</c>.</summary>
        protected IGenericRepository<T> Repo => Repository<T>();

        /// <summary>List only rows that are active and not soft-deleted.</summary>
        public Task<IReadOnlyList<T>> GetAllActiveAsync() =>
            Repo.GetByConditionAsync(x => x.DeletedAt == null && x.IsActive);

        /// <summary>Fetch one row or throw <see cref="NotFoundException"/>.</summary>
        public async Task<T> MustGetAsync(int id)
        {
            var e = await Repo.GetByIdAsync(id);
            if (e is null || e.DeletedAt.HasValue)
                throw new NotFoundException(typeof(T).Name, id);
            return e;
        }

        /// <summary>Create a new active entity with create-audit stamped.</summary>
        protected Task<int> CreateWithAuditAsync(T entity)
        {
            ApplyCreateAudit(entity);
            _ = Repo.AddAsync(entity);
            return SaveAsync();
        }

        /// <summary>Soft-delete a master row (audit stamped).</summary>
        protected async Task SoftDeleteByIdAsync(int id)
        {
            var e = await MustGetAsync(id);
            ApplySoftDelete(e);
            Repo.Update(e);
            await SaveAsync();
        }
    }
}
