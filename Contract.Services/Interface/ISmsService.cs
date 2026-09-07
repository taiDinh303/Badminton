namespace Contract.Services.Interface
{
    public interface ISmsService
    {
        Task SendVerificationCodeAsync(
            string phoneNumber,
            string code);
    }
}