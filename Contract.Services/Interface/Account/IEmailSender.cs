
namespace Contract.Services.Interface
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string to, string subject, string htmlBody);
    }

}
