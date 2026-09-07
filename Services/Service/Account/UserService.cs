using Contract.Repositories.Entity;
using Contract.Repositories.IUnitOfWork;
using Contract.Services.Interface;
using Core.Base;
using Core.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ModelViews.UserInfoModelView;
using ModelViews.UserModelView;
using Services.Mappings;
using System.Data;
using System.Text;
using static Core.Base.BaseException;

namespace Services.Service
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUserInfoService _userInfoService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public UserService(IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager, IHttpContextAccessor httpContextAccessor, IUserInfoService userInfoService, IEmailService emailService, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _userInfoService = userInfoService;
            _emailService = emailService;
            _configuration = configuration;
        }


        #region Create - Get - Update - Delete

        public async Task CreateAsync(CreateUserModelView model)
        {
            ApplicationUser user = new ApplicationUser
            {
                UserName = model.Username,
                Email = model.Email,
                EmailConfirmed = true
            };


            // Create Identity User
            IdentityResult result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                string error = result.Errors.FirstOrDefault()?.Description
                               ?? "Unknown error occurred";

                throw new ErrorException(
                    400,
                    "INVALID_INPUT",
                    error
                );
            }


            // Check role exists
            bool roleExists = await _userManager.IsInRoleAsync(user, model.Role);

            if (!roleExists)
            {
                IdentityResult roleResult = await _userManager.AddToRoleAsync(
                    user,
                    model.Role
                );

                if (!roleResult.Succeeded)
                {
                    string error = roleResult.Errors.FirstOrDefault()?.Description
                                   ?? "Failed to assign role";

                    throw new ErrorException(
                        400,
                        "ROLE_ERROR",
                        error
                    );
                }
            }


            // Create UserInfo
            if (model.CreateUserInfoModelView != null)
            {
                model.CreateUserInfoModelView.UserId = user.Id;

                await _userInfoService.CreateAsync(
                    model.CreateUserInfoModelView
                );
            }
            else
            {
                var defaultUserInfo = new CreateUserInfoModelView
                {
                    UserId = user.Id,
                    GivenName = user.UserName ?? "User",
                    BirthDate = DateTime.UtcNow,
                    Gender = GenderType.RatherNotSay
                };

                await _userInfoService.CreateAsync(defaultUserInfo);
            }
        }


        public async Task<BasePaginatedList<UserResponseModelView>> GetAllAsync(int pageNumber, int pageSize)
        {
            IGenericRepository<ApplicationUser> userRepo = _unitOfWork.GetRepository<ApplicationUser>();

            IQueryable<ApplicationUser> query = userRepo.Entities
                .Where(u => !u.DeletedTime.HasValue)
                .OrderBy(u => u.CreatedTime);

            int totalItems = await query.CountAsync();

            List<ApplicationUser> users = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            List<UserResponseModelView> result = users.ToViewModelList();

            return new BasePaginatedList<UserResponseModelView>(
                result.AsReadOnly(),
                totalItems,
                pageNumber,
                pageSize
            );
        }

        public async Task<UserResponseModelView> GetByIdAsync(Guid id)
        {
            ApplicationUser? user = await _unitOfWork.GetRepository<ApplicationUser>().Entities.Where(u => u.Id == id && !u.DeletedTime.HasValue).FirstOrDefaultAsync()
                ?? throw new ErrorException(
                    StatusCodes.Status404NotFound,
                    ResponseCodeConstants.NOT_FOUND,
                    "User not found"
                );

            // Mapping
            UserResponseModelView userViewModel = user.ToViewModel();
            return userViewModel;
        }

        public async Task UpdateAsync(UpdateUserModelView request)
        {
            var userRepo = _unitOfWork.GetRepository<ApplicationUser>();
            ApplicationUser user = await userRepo.Entities
                .FirstOrDefaultAsync(u => u.Id == request.Id && !u.DeletedTime.HasValue)
                ?? throw new ErrorException(
                    StatusCodes.Status404NotFound,
                    ResponseCodeConstants.NOT_FOUND,
                    "User not found"
                );

            // Update
            request.ToEntity(user);
            user.LastUpdatedBy = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
            user.LastUpdatedTime = CoreHelper.SystemTimeNow;

            await userRepo.UpdateAsync(user);
        }

        #region Delete
        public async Task SoftDeleteAsync(Guid id)
        {
            IGenericRepository<ApplicationUser> userRepo = _unitOfWork.GetRepository<ApplicationUser>();
            ApplicationUser? user = await userRepo.Entities
                .FirstOrDefaultAsync(u => u.Id == id && !u.DeletedTime.HasValue)
                ?? throw new ErrorException(
                    StatusCodes.Status404NotFound,
                    ResponseCodeConstants.NOT_FOUND,
                    "User not found"
                );

            // delete UserInfo
            await _userInfoService.DeleteAsync(id);

            //audit
            string currentUser = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
            user.DeletedBy = currentUser;
            user.DeletedTime = CoreHelper.SystemTimeNow;

            await userRepo.UpdateAsync(user);
            await _unitOfWork.SaveAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            ApplicationUser user = await _userManager.FindByIdAsync(id.ToString())
                ?? throw new ErrorException(
                    StatusCodes.Status404NotFound,
                    ResponseCodeConstants.NOT_FOUND,
                    "User not found"
                );

            // hard delete UserInfo
            await _userInfoService.DeleteAsync(id);

            // Xóa luôn user
            var result = await _userManager.DeleteAsync(user);

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
        #endregion


        #endregion





        #region Update


        public async Task ConfirmChangeEmailAsync(Guid userId, string token, string newEmail)
        {
            // Lấy user bằng UserManager
            ApplicationUser user = await _userManager.FindByIdAsync(userId.ToString())
                ?? throw new ErrorException(
                    StatusCodes.Status404NotFound,
                    ResponseCodeConstants.NOT_FOUND,
                    "User not found"
                );

            if (string.IsNullOrEmpty(newEmail))
                throw new ErrorException(
                    StatusCodes.Status400BadRequest,
                    ResponseCodeConstants.INVALID_INPUT,
                    "New email cannot be empty"
                );

            // Decode token
            var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));

            // Thay đổi email
            var result = await _userManager.ChangeEmailAsync(user, newEmail, decodedToken);
            if (!result.Succeeded)
            {
                string errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new ErrorException(
                    StatusCodes.Status400BadRequest,
                    ResponseCodeConstants.FAILED,
                    $"Email confirmation failed: {errors}"
                );
            }

            // Cập nhật NormalizedEmail (IdentityManager cũng làm việc này, nhưng để chắc chắn)
            user.NormalizedEmail = newEmail.ToUpperInvariant();

            // Cập nhật audit
            user.LastUpdatedBy = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
            user.LastUpdatedTime = CoreHelper.SystemTimeNow;

            // Cập nhật SecurityStamp để invalidate session/tokens cũ
            await _userManager.UpdateSecurityStampAsync(user);

            // Lưu thay đổi user
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                string errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                throw new ErrorException(
                    StatusCodes.Status500InternalServerError,
                    ResponseCodeConstants.FAILED,
                    $"Failed to update user after email change: {errors}"
                );
            }
        }


        #endregion



        #region EmailManagement
        // Send change-email confirmation email
        public async Task SendChangeEmailAsync(UpdateEmailModelView request)
        {
            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Id == request.Id && !u.DeletedTime.HasValue)
                ?? throw new ErrorException(
                    StatusCodes.Status404NotFound,
                    ResponseCodeConstants.NOT_FOUND,
                    "User not found"
                );

            // Check if email already exists for another user
            bool emailExists = await _userManager.Users
                .AnyAsync(u => u.Email == request.Email
                               && u.Id != request.Id
                               && !u.DeletedTime.HasValue);

            if (emailExists)
            {
                throw new ErrorException(
                    StatusCodes.Status400BadRequest,
                    ResponseCodeConstants.EXISTED,
                    "Email already exists."
                );
            }

            // Create email change token
            var token = await _userManager.GenerateChangeEmailTokenAsync(user, request.Email);

            // Send email confirmation
            await _emailService.SendChangeEmailConfirmationAsync(user, token, request.Email);
        }


        // Send a set-password email to a user who doesn't have a password yet
        public async Task SendSetPasswordLinkAsync(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString())
                ?? throw new ErrorException(
                    StatusCodes.Status404NotFound,
                    ResponseCodeConstants.NOT_FOUND,
                    "User not found"
                );

            if (user.PasswordHash != null)
                throw new ErrorException(
                    StatusCodes.Status400BadRequest,
                    ResponseCodeConstants.INVALID_INPUT,
                    "User already has a password"
                );
            // Create password reset token
            string token = await _userManager.GeneratePasswordResetTokenAsync(user);
            // Send email confirmation
            await _emailService.SendSetPasswordLinkAsync(user, token);
        }
        #endregion




        #region Password Management

        // Set password if user didn't have one(e.g., Google login)
        public async Task SetPasswordAsync(Guid userId, string token, string newPassword)
        {
            ApplicationUser user = await _userManager.FindByIdAsync(userId.ToString())
                ?? throw new ErrorException(
                    StatusCodes.Status404NotFound,
                    ResponseCodeConstants.NOT_FOUND,
                    "User not found"
                );

            IdentityResult result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (!result.Succeeded)
            {
                string errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new ErrorException(
                    StatusCodes.Status400BadRequest,
                    ResponseCodeConstants.FAILED,
                    $"{errors}"
                );
            }

            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);
        }

        // Change password when user knows the current password
        public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
        {
            ApplicationUser user = await _userManager.FindByIdAsync(userId.ToString())
                ?? throw new ErrorException(
                    StatusCodes.Status404NotFound,
                    ResponseCodeConstants.NOT_FOUND,
                    "User not found."
                );

            // Dùng Identity ChangePasswordAsync để đảm bảo current password đúng
            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

            if (!result.Succeeded)
            {
                string errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new ErrorException(
                    StatusCodes.Status400BadRequest,
                    ResponseCodeConstants.FAILED,
                    errors
                );
            }

            // Cập nhật audit
            user.LastUpdatedBy = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
            user.LastUpdatedTime = CoreHelper.SystemTimeNow;

            await _userManager.UpdateAsync(user);
        }



        #endregion






    }
}
