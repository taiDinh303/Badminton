using Contract.Repositories.Entity;

namespace Contract.Repositories.IOUW
{
    public interface IBankAccountRepository
    {
        Task<List<BankAccount>> GetByUserIdAsync(int userId);

        Task<BankAccount?> GetByIdAsync(int id);

        Task<BankAccount> CreateAsync(BankAccount bankAccount);

        Task<BankAccount?> UpdateAsync(BankAccount bankAccount);

        Task<bool> DeleteAsync(int id);
    }
}