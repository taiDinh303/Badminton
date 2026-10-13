using Contract.Repositories.IUnitOfWork;
using Core.Abstractions;
using Core.Base;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;

namespace Services.Base
{
    /// <summary>
    /// Abstract base service – đóng gói các thao tác dùng chung
    /// (truy cập unit of work, quyền, user hiện tại, entity mềm-delete).
    /// Mọi service cụ thể kế thừa để tránh lặp (encapsulation + inheritance).
    ///
    /// Thiết kế:
    ///  - IUnitOfWork được injected protected, service con dùng thẳng.
    ///  - CurrentUserId / IsAdmin được tính từ HttpContext (JWT claims).
    ///  - FindByIdAsync ném NotFound khi không tìm thấy.
    /// </summary>
    public abstract class BaseService<T> : BaseService where T : class
    {
        protected readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;

        protected BaseService(
            IUnitOfWork unitOfWork,
            IHttpContextAccessor httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <inheritdoc />
        protected override string? CurrentUserId
            => _httpContextAccessor.HttpContext?.User
                .FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        /// <inheritdoc />
        protected override bool IsAdmin
            => _httpContextAccessor.HttpContext?.User.IsInRole("Admin") == true;

        /// <summary>Repository của entity T.</summary>
        protected IGenericRepository<T> Repository => _unitOfWork.GetRepository<T>();

        /// <summary>Tìm entity chưa xóa theo ID, ném NotFound.</summary>
        protected async Task<T> FindByIdAsync(Guid id)
        {
            var entity = await Repository.FindByConditionAsync(
                e => e.Id == id);

            if (entity is null || Deleted(e) != null)
                throw NotFound(typeof(T).Name, id.ToString());

            return entity;
        }

        /// <summary>Kiểm tra entity đã bị soft-delete chưa.</summary>
        protected static DateTimeOffset? Deleted(T entity)
            => entity is BaseEntity be ? be.DeletedTime : null;

        /// <summary>Thực hiện soft-delete: set DeletedTime/DeletedBy.</summary>
        protected void MarkDeleted(T entity)
        {
            if (entity is BaseEntity be)
            {
                be.DeletedTime = Core.Utils.CoreHelper.SystemTimeNow;
                be.DeletedBy = CurrentUserId;
            }
        }

        /// <summary>
        /// Chặn ghi đè trực tiếp thuộc tính của entity (encapsulation).
        /// Service con phải dùng UpdateFields thay vì sửa entity trần.
        /// </summary>
        protected void UpdateAuditFields(T entity)
        {
            if (entity is BaseEntity be)
            {
                be.LastUpdatedBy = CurrentUserId;
                be.LastUpdatedTime = Core.Utils.CoreHelper.SystemTimeNow;
            }
        }
    }
}
