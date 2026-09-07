

using Contract.Repositories.Entity;

namespace Contract.Services.Interface
{
    public interface IEmailService
    {
        Task SendVerificationCodeAsync(string email, string code);

        Task SendChangeEmailConfirmationAsync(ApplicationUser user, string token, string newEmail);

        Task SendSetPasswordLinkAsync(ApplicationUser user, string token);

        Task SendForgotPasswordLinkAsync(ApplicationUser user, string token);


    }

}
