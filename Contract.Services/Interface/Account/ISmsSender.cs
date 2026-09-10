namespace Contract.Services.Interface
{
    public interface ISmsSender
    {
        Task SendSmsAsync(string phoneNumber, string message);

        Task SendTestSmsAsync(string phoneNumber, string message);

    }
}