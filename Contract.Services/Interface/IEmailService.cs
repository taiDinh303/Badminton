namespace Contract.Services.Interface
{
    public interface IEmailService
    {
        Task SendVerificationCodeAsync(
            string email,
            string fullName,
            string code);
    }
}