namespace Contract.Services.Interface
{
    public interface IVerificationService
    {
        Task SendEmailVerificationAsync(int userId);

        Task SendPhoneVerificationAsync(int userId);

        Task VerifyEmailAsync(int userId, string code);

        Task VerifyPhoneAsync(int userId, string code);
    }
}