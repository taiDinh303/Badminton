using Contract.Repositories.Entity;
using Contract.Repositories.IUnitOfWork;
using Contract.Services.Interface;
using Core.Base;
using Core.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ModelViews.RoleModelView;
using Services.Mappings;
using static Core.Base.BaseException;

namespace Services.Service
{
    public class RoleService : IRoleService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public RoleService(IUnitOfWork unitOfWork, RoleManager<ApplicationRole> roleManager, IHttpContextAccessor httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _roleManager = roleManager;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<BasePaginatedList<RoleResponseModelView>> GetAllAsync(int pageNumber, int pageSize)
        {
            IGenericRepository<ApplicationRole> repo = _unitOfWork.GetRepository<ApplicationRole>();
            IQueryable<ApplicationRole> query = repo.Entities
                .Where(r => !r.DeletedTime.HasValue)
                .OrderBy(u => u.CreatedTime);

            int totalItems = await query.CountAsync();

            List<ApplicationRole> roles = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize).ToListAsync();

            List<RoleResponseModelView> result = roles.ToViewModelList();

            return new BasePaginatedList<RoleResponseModelView>(
                result.AsReadOnly(),
                totalItems,
                pageNumber,
                pageSize);
        }

        public async Task<RoleResponseModelView> GetByIdAsync(Guid id)
        {
            ApplicationRole? role = await _unitOfWork.GetRepository<ApplicationRole>().Entities
                .Where(r => r.Id == id && !r.DeletedTime.HasValue)
                .FirstOrDefaultAsync()
                ?? throw new ErrorException(
                    StatusCodes.Status404NotFound,
                    ResponseCodeConstants.NOT_FOUND,
                    "Role not found"
                );

            return role.ToViewModel();
        }


        public async Task CreateAsync(CreateRoleModelView request)
        {
            ApplicationRole? existingRole = await _roleManager.FindByNameAsync(request.Name ?? string.Empty);
            if (existingRole != null && !existingRole.DeletedTime.HasValue)
            {
                throw new ErrorException(
                    StatusCodes.Status400BadRequest,
                    ResponseCodeConstants.EXISTED,
                    "Role already exists"
                );
            }

            ApplicationRole newRole = request.ToEntity();

            IdentityResult result = await _roleManager.CreateAsync(newRole);
            if (!result.Succeeded)
            {
                string errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new ErrorException(StatusCodes.Status400BadRequest, ResponseCodeConstants.FAILED, errors);
            }
        }

        public async Task UpdateAsync(UpdateRoleModelView request)
        {
            ApplicationRole? role = await _roleManager.FindByIdAsync(request.Id.ToString());
            if (role == null || role.DeletedTime.HasValue)
            {
                throw new ErrorException(
                    StatusCodes.Status404NotFound,
                    ResponseCodeConstants.NOT_FOUND,
                    "Role not found"
                );
            }

            if (!string.IsNullOrEmpty(request.Name))
            {
                role.Name = request.Name;
            }

            if (!string.IsNullOrEmpty(request.Description))
            {
                role.Description = request.Description;
            }

            //Audit
            string? currentUser = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            role.LastUpdatedBy = currentUser ?? "System";
            role.LastUpdatedTime = CoreHelper.SystemTimeNow;

            IdentityResult result = await _roleManager.UpdateAsync(role);
            if (!result.Succeeded)
            {
                string errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new ErrorException(StatusCodes.Status400BadRequest, ResponseCodeConstants.FAILED, errors);
            }
        }

        public async Task SoftDeleteAsync(Guid id)
        {
            IGenericRepository<ApplicationRole> roleRepo = _unitOfWork.GetRepository<ApplicationRole>();
            ApplicationRole role = await roleRepo.Entities
                .FirstOrDefaultAsync(r => r.Id == id && !r.DeletedTime.HasValue)
                ?? throw new ErrorException(StatusCodes.Status404NotFound, ResponseCodeConstants.NOT_FOUND, "Role not found");

            //Audit
            string currentUser = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
            role.DeletedBy = currentUser;
            role.DeletedTime = CoreHelper.SystemTimeNow;

            await roleRepo.UpdateAsync(role);
            await _unitOfWork.SaveAsync();
        }


        public async Task DeleteAsync(Guid id)
        {
            ApplicationRole role = await _roleManager.FindByIdAsync(id.ToString())
                ?? throw new ErrorException(
                    StatusCodes.Status404NotFound,
                    ResponseCodeConstants.NOT_FOUND,
                    "Role not found"
            );

            //delete by roleManager
            IdentityResult result = await _roleManager.DeleteAsync(role);

            if (!result.Succeeded)
            {
                string errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new ErrorException(
                    StatusCodes.Status400BadRequest,
                    ResponseCodeConstants.FAILED,
                    errors
                );
            }
        }
    }
}
