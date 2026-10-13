using Contract.Repositories.UnitOfWork;
using Microsoft.AspNetCore.Http;
using M.Core.Base;

namespace Services
{
    public abstract class BaseService
    {
        protected readonly IUnitOfWork Uow;
        protected readonly IHttpContextAccessor _http;

        protected BaseService(IUnitOfWork uow, IHttpContextAccessor http)
        {
            Uow = uow;
            _http = http;
        }

        protected IGenericRepository<T> Repository<T>() where T : class => Uow.GetRepository<T>();

        protected string CurrentUserName =>
            _http.HttpContext?.User?.Identity?.Name ?? "system";

        protected Task<int> SaveAsync() => Uow.SaveChangesAsync();
    }
}
