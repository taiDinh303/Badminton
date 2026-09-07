namespace Contract.Services.Interface
{
    public interface INotificationService
    {
        Task SendEmailConfirmationAsync(string email);
    }
}
