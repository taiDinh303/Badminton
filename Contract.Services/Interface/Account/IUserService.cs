using Core.Base;
using ModelViews.UserInfoModelView;
using ModelViews.UserModelView;

namespace Contract.Services.Interface
{
    public interface IUserService
    {
        Task<BasePaginatedList<UserResponseModelView>> GetAllAsync(int pageNumber, int pageSize);
        Task<UserResponseModelView> GetByIdAsync(Guid id);

        Task CreateAsync(CreateUserModelView model);

        Task UpdateAsync(UpdateUserModelView user);

        Task ConfirmChangeEmailAsync(Guid userId, string token, string newEmail);

        Task SetPasswordAsync(Guid userId, string token, string newPassword);

        Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);


        // Send change-email confirmation email
        Task SendChangeEmailAsync(UpdateEmailModelView request);
        // Send a set-password email to a user who doesn't have a password yet
        Task SendSetPasswordLinkAsync(Guid userId);



        Task SoftDeleteAsync(Guid id);
        Task DeleteAsync(Guid id);




    }
}
