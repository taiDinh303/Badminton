using Contract.Repositories.UnitOfWork;
using Badminton.Core.Security;
using Badminton.Core.Base;
using Badminton.Core.Enums;
using Contract.Repositories.Entity;

namespace Services
{
    public abstract class BaseService
    {
        protected readonly IUnitOfWork Uow;
        protected readonly ICurrentUser _currentUser;

        protected BaseService(IUnitOfWork uow, ICurrentUser currentUser)
        {
            Uow = uow;
            _currentUser = currentUser;
        }

        protected IGenericRepository<T> Repository<T>() where T : class => Uow.GetRepository<T>();

        protected string CurrentUserName =>
            _currentUser.Principal.Identity?.Name ?? "system";

        protected async Task<int?> ResolveStaffBranchAsync(int? requestedBranch)
        {
            var actor = await Repository<User>().GetByIdAsync(_currentUser.Id);
            if (actor is null || actor.DeletedAt.HasValue || actor.Status != UserStatus.Active)
                throw new ConflictException("Tài khoản nhân viên không còn hoạt động.");
            if (actor.Role == Role.Admin && _currentUser.IsInRole("Admin")) return requestedBranch;
            if (actor.Role != Role.Manager || !_currentUser.IsInRole("Manager") || actor.BranchId is not { } branch)
                throw new ConflictException("Tài khoản không có quyền xử lý cơ sở này.");
            if (requestedBranch.HasValue && requestedBranch != branch)
                throw new ConflictException("Bạn chỉ được xử lý dữ liệu thuộc cơ sở của mình.");
            return branch;
        }

        protected async Task AuthorizeStaffBranchAsync(int branch, int actorId)
        {
            if (_currentUser.Id != actorId) throw new ConflictException("Danh tính thao tác không hợp lệ.");
            await ResolveStaffBranchAsync(branch);
        }

        protected Task<int> SaveAsync() => Uow.SaveChangesAsync();
    }
}
