using Contract.Services.Interface;
using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Services.Service
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendVerificationCodeAsync(
            string email,
            string fullName,
            string code)
        {
            var apiKey = _configuration["SendGridSettings:ApiKey"];
            var fromEmail = _configuration["SendGridSettings:FromEmail"];
            var fromName = _configuration["SendGridSettings:FromName"];

            var client = new SendGridClient(apiKey);

            var from = new EmailAddress(
                fromEmail,
                fromName);

            var to = new EmailAddress(
                email,
                fullName);

            var subject = "Xác thực tài khoản Badminton Booking";

            var plainText =
                $"Xin chào {fullName}, mã xác thực của bạn là: {code}. Mã có hiệu lực trong 5 phút.";

            var html =
                $"""
                <h2>Badminton Booking</h2>
                <p>Xin chào <strong>{fullName}</strong>,</p>
                <p>Mã xác thực của bạn là:</p>
                <h1>{code}</h1>
                <p>Mã có hiệu lực trong <strong>5 phút</strong>.</p>
                <p>Không chia sẻ mã này với người khác.</p>
                """;

            var message = MailHelper.CreateSingleEmail(
                from,
                to,
                subject,
                plainText,
                html);

            var response = await client.SendEmailAsync(message);

            if ((int)response.StatusCode < 200 ||
                (int)response.StatusCode >= 300)
            {
                var body = await response.Body.ReadAsStringAsync();

                throw new Exception(
                    $"Gửi email thất bại: {body}");
            }
        }
    }
}