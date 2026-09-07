
using Contract.Repositories.Entity;
using Contract.Repositories.IUnitOfWork;
using Contract.Services.Interface;
using Core.Base;
using Core.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ModelViews.UserInfoModelView;
using Services.Mappings;
using static Core.Base.BaseException;

namespace Services.Service
{
    public class UserInfoService : IUserInfoService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserInfoService(IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager, IHttpContextAccessor httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<BasePaginatedList<UserInfoResponseModelView>> GetAllAsync(int pageNumber, int pageSize)
        {
            IGenericRepository<UserInfo> repo = _unitOfWork.GetRepository<UserInfo>();
            IQueryable<UserInfo> query = repo.Entities
                .Where(x => !x.DeletedTime.HasValue)
                .OrderBy(x => x.CreatedTime);

            int totalItems = await query.CountAsync();

            List<UserInfo> userInfos = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            List<UserInfoResponseModelView> result = userInfos
                .Select(x => x.ToViewModel())
                .ToList();

            return new BasePaginatedList<UserInfoResponseModelView>(
                result.AsReadOnly(),
                totalItems,
                pageNumber,
                pageSize
                );
        }

        public async Task<UserInfoResponseModelView> GetByIdAsync(Guid id)
        {
            IGenericRepository<UserInfo> repo = _unitOfWork.GetRepository<UserInfo>();

            UserInfo userInfo = await repo.Entities
                .Where(x => x.Id == id && !x.DeletedTime.HasValue)
                .FirstOrDefaultAsync()
                ?? throw new ErrorException(
                    StatusCodes.Status404NotFound,
                    "NOT_FOUND",
                    "UserInfo not found"
                );

            // Mapping ModelView
            UserInfoResponseModelView userInfoViewModel = userInfo.ToViewModel();
            return userInfoViewModel;
        }


        public async Task CreateAsync(CreateUserInfoModelView model)
        {
            var repo = _unitOfWork.GetRepository<UserInfo>();

            // Kiểm tra user đã có UserInfo chưa
            bool exists = await repo.Entities.AnyAsync(x => x.Id == model.UserId && !x.DeletedTime.HasValue);
            if (exists)
            {
                throw new ErrorException(
                    StatusCodes.Status400BadRequest,
                    "DUPLICATE",
                    "UserInfo already exists for this user"
                );
            }
            // Audit
            string? currentUsername = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            var userInfo = new UserInfo
            {
                Id = model.UserId,
                GivenName = model.GivenName?.Trim() ?? string.Empty,
                FamilyName = model.FamilyName?.Trim(),
                Picture = model.Picture,
                BirthDate = model.BirthDate,
                Gender = model.Gender,
                CreatedBy = currentUsername ?? "System",
                CreatedTime = CoreHelper.SystemTimeNow
            };

            await repo.InsertAsync(userInfo);
            await _unitOfWork.SaveAsync();
        }



        public async Task UpdateAsync(UpdateUserInfoModelView model)
        {
            IGenericRepository<UserInfo> repo = _unitOfWork.GetRepository<UserInfo>();
            UserInfo userInfo = await repo.Entities.FirstOrDefaultAsync(x => x.Id == model.UserId)
                ?? throw new ErrorException(StatusCodes.Status404NotFound, "NOT_FOUND", "UserInfo not found");

            //Audit
            string? currentUsername = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            userInfo.LastUpdatedBy = currentUsername ?? "System";
            userInfo.LastUpdatedTime = CoreHelper.SystemTimeNow;

            model.ToEntity(userInfo);
            await repo.UpdateAsync(userInfo);
            await _unitOfWork.SaveAsync();
        }

        public async Task SoftDeleteAsync(Guid id)
        {
            IGenericRepository<UserInfo> repo = _unitOfWork.GetRepository<UserInfo>();

            UserInfo userInfo = await repo.Entities
                .FirstOrDefaultAsync(x => x.Id == id && !x.DeletedTime.HasValue)
                ?? throw new ErrorException(
                    StatusCodes.Status404NotFound,
                    "NOT_FOUND",
                    "UserInfo not found"
                );

            string currentUser = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
            userInfo.DeletedBy = currentUser;
            userInfo.DeletedTime = CoreHelper.SystemTimeNow;

            await repo.UpdateAsync(userInfo);
            await _unitOfWork.SaveAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            IGenericRepository<UserInfo> repo = _unitOfWork.GetRepository<UserInfo>();

            UserInfo userInfo = await repo.Entities
                .FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new ErrorException(
                    StatusCodes.Status404NotFound,
                    "NOT_FOUND",
                    "UserInfo not found"
                );

            await repo.DeleteAsync(userInfo);
            await _unitOfWork.SaveAsync();
        }


        #region Update Specific Fields
        public async Task UpdateNameAsync(UpdateNameModelView model)
        {
            var repo = _unitOfWork.GetRepository<UserInfo>();
            var userInfo = await repo.Entities.FirstOrDefaultAsync(x => x.Id == model.UserId)
                ?? throw new ErrorException(
                    StatusCodes.Status404NotFound,
                    ResponseCodeConstants.NOT_FOUND,
                    "UserInfo not found."
                );

            // Validate
            if ((!string.IsNullOrWhiteSpace(model.GivenName) && model.GivenName.Length > 25) ||
                (!string.IsNullOrWhiteSpace(model.FamilyName) && model.FamilyName.Length > 25))
            {
                throw new ErrorException(
                    StatusCodes.Status400BadRequest,
                    ResponseCodeConstants.INVALID_INPUT,
                    "Name is too long."
                );
            }

            // Update
            userInfo.GivenName = model.GivenName;
            userInfo.FamilyName = model.FamilyName;
            userInfo.LastUpdatedBy = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
            userInfo.LastUpdatedTime = CoreHelper.SystemTimeNow;

            await repo.UpdateAsync(userInfo);
            await _unitOfWork.SaveAsync();
        }

        public async Task UpdateGenderAsync(UpdateGenderModelView model)
        {
            var repo = _unitOfWork.GetRepository<UserInfo>();
            var userInfo = await repo.Entities.FirstOrDefaultAsync(x => x.Id == model.UserId)
                ?? throw new ErrorException(
                    StatusCodes.Status404NotFound,
                    ResponseCodeConstants.NOT_FOUND,
                    "UserInfo not found."
                );


            userInfo.Gender = model.Gender;
            userInfo.LastUpdatedBy = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
            userInfo.LastUpdatedTime = CoreHelper.SystemTimeNow;

            await repo.UpdateAsync(userInfo);
            await _unitOfWork.SaveAsync();
        }

        public async Task UpdateBirthdayAsync(UpdateBirthdayModelView model)
        {
            var repo = _unitOfWork.GetRepository<UserInfo>();
            var userInfo = await repo.Entities.FirstOrDefaultAsync(x => x.Id == model.UserId)
                ?? throw new ErrorException(
                    StatusCodes.Status404NotFound,
                    ResponseCodeConstants.NOT_FOUND,
                    "UserInfo not found."
                );

            if (model.BirthDate > CoreHelper.SystemTimeNow)
                throw new ErrorException(
                    StatusCodes.Status400BadRequest,
                    ResponseCodeConstants.INVALID_INPUT,
                    "Birth date cannot be in the future."
                );

            userInfo.BirthDate = model.BirthDate;
            userInfo.LastUpdatedBy = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
            userInfo.LastUpdatedTime = CoreHelper.SystemTimeNow;

            await repo.UpdateAsync(userInfo);
            await _unitOfWork.SaveAsync();
        }

        #endregion








    }
}
