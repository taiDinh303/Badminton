using Contract.Repositories.Entity;

namespace Contract.Repositories.IOUW
{
    public interface IVerificationCodeRepository
    {
        Task<VerificationCode?> GetLatestAsync(
            int userId,
            string type);

        Task CreateAsync(VerificationCode verificationCode);

        Task UpdateAsync(VerificationCode verificationCode);
    }
}
