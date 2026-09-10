namespace Contract.Services.Interface
{
    public interface IOtpService
    {
        Task StoreAsync(string key, string code, DateTime expiration);
        Task<bool> ValidateAsync(string key, string code);
        string GenerateConfirmationCode();
    }


}
