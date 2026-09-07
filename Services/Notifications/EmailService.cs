using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text;
using Contract.Repositories.AuthEntity;
using Contract.Services.Interface;
using Services.Infrastructure;

namespace Services.Notifications
{
    public class EmailService : IEmailService
    {
        private readonly IEmailSender _sender;
        private readonly EmailTemplateRenderer _renderer;
        private readonly string _clientUrl;

        public EmailService(IEmailSender sender, EmailTemplateRenderer renderer, IConfiguration config)
        {
            _sender = sender;
            _renderer = renderer;
            _clientUrl = config["AppSettings:ClientUrl"]
                          ?? throw new Exception("ClientUrl is not configured");
        }

        public async Task SendVerificationCodeAsync(string email, string code)
        {
            string html = _renderer.RenderVerificationCode(code);
            await _sender.SendEmailAsync(email, "Your Verification Code", html);
        }

        public async Task SendChangeEmailConfirmationAsync(ApplicationUser user, string token, string newEmail)
        {
            var safeToken = WebUtility.UrlEncode(
                WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token)));

            string safeEmail = WebUtility.UrlEncode(newEmail);

            string link = $"{_clientUrl}/auth/confirm-change-email?userId={user.Id}&token={safeToken}&newEmail={safeEmail}";

            string html = _renderer.RenderChangeEmail(user.UserName!, link, newEmail);
            await _sender.SendEmailAsync(newEmail, "Confirm New Email", html);
        }

        //
        public async Task SendSetPasswordLinkAsync(ApplicationUser user, string token)
        {
            var safeToken = WebUtility.UrlEncode(
                WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token)));

            string link = $"{_clientUrl}/auth/set-password?userId={user.Id}&token={safeToken}";

            string html = _renderer.RenderSetPasswordLink(user.UserName!, link);
            await _sender.SendEmailAsync(user.Email!, "Set Password for Your Account", html);
        }

        //
        public async Task SendForgotPasswordLinkAsync(ApplicationUser user, string token)
        {
            var safeToken = WebUtility.UrlEncode(
                WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token)));

            string link = $"{_clientUrl}/auth/set-password?userId={user.Id}&token={safeToken}";

            string html = _renderer.RenderForgotPassword(user.UserName!, link);
            await _sender.SendEmailAsync(user.Email!, "Reset Your Password", html);
        }












    }
}

