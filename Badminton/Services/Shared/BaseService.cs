using Contract.Repositories.UnitOfWork;
using M.Core.Base;
using Microsoft.AspNetCore.Http;

namespace Services
{
    /// <summary>
    /// ABSTRACTION + INHERITANCE + ENCAPSULATION: the root of every concrete
    /// business service. It holds the <c>IUnitOfWork</c> (transaction boundary)
    /// and the <c>IHttpContextAccessor</c>, and exposes a <c>Repository&lt;T&gt;()</c>
    /// helper so derived services never touch EF Core or the UoW directly.
    /// The audit / soft-delete template methods centralise the rules so no
    /// concrete service forgets to stamp them.
    /// </summary>
    public abstract class BaseService
    {
        protected readonly IUnitOfWork Uow;
        protected readonly IHttpContextAccessor _http;

        protected BaseService(IUnitOfWork uow, IHttpContextAccessor http)
        {
            Uow = uow;
            _http = http;
        }

        /// <summary>Encapsulation: pull a repository for any entity type.</summary>
        protected IGenericRepository<T> Repository<T>() where T : class =>
            Uow.GetRepository<T>();

        /// <summary>Current authenticated user name, or "system" when unauthenticated.</summary>
        protected string CurrentUserName =>
            _http.HttpContext?.User?.Identity?.Name ?? "system";

        /// <summary>Apply create-audit to a tracked entity.</summary>
        protected void ApplyCreateAudit<T>(T entity) where T : IntBaseEntity =>
            entity.CreatedAt = DateTime.UtcNow;

        /// <summary>Apply update-audit to a tracked entity.</summary>
        protected void ApplyUpdateAudit<T>(T entity) where T : IntBaseEntity =>
            entity.UpdatedAt = DateTime.UtcNow;

        /// <summary>Apply a soft-delete marker instead of a hard delete.</summary>
        protected void ApplySoftDelete<T>(T entity) where T : IntBaseEntity
        {
            entity.DeletedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>Persist all pending changes for this unit of work.</summary>
        protected Task<int> SaveAsync() => Uow.SaveChangesAsync();
    }
}
