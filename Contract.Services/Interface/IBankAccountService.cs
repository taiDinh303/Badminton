using ModelViews.BankAccount;

namespace Contract.Services.Interface
{
    public interface IBankAccountService
    {
        Task<List<BankAccountResponse>> GetByUserIdAsync(int userId);

        Task<BankAccountResponse?> GetByIdAsync(
            int id,
            int userId);

        Task<BankAccountResponse> CreateAsync(
            CreateBankAccountRequest request,
            int userId);

        Task<BankAccountResponse?> UpdateAsync(
            int id,
            UpdateBankAccountRequest request,
            int userId);

        Task<bool> DeleteAsync(
            int id,
            int userId);
    }
}