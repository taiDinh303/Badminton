using Contract.Repositories.Entity;
using Contract.Repositories.IOUW;
using Contract.Services.Interface;
using ModelViews.BankAccount;

namespace Services.Service
{
    public class BankAccountService : IBankAccountService
    {
        private readonly IUOW _uow;

        public BankAccountService(IUOW uow)
        {
            _uow = uow;
        }

        public async Task<List<BankAccountResponse>> GetByUserIdAsync(int userId)
        {
            var accounts = await _uow.BankAccounts.GetByUserIdAsync(userId);

            return accounts.Select(MapToResponse).ToList();
        }

        public async Task<BankAccountResponse?> GetByIdAsync(int id, int userId)
        {
            var account = await _uow.BankAccounts.GetByIdAsync(id);

            if (account == null || account.UserId != userId)
                return null;

            return MapToResponse(account);
        }

        public async Task<BankAccountResponse> CreateAsync(
            CreateBankAccountRequest request,
            int userId)
        {
            var account = new BankAccount
            {
                UserId = userId,
                BankName = request.BankName,
                AccountNumber = request.AccountNumber,
                AccountHolder = request.AccountHolder,
                AccountType = request.AccountType,
                IsDefault = false,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _uow.BankAccounts.CreateAsync(account);

            return MapToResponse(result);
        }

        public async Task<BankAccountResponse?> UpdateAsync(
            int id,
            UpdateBankAccountRequest request,
            int userId)
        {
            var account = await _uow.BankAccounts.GetByIdAsync(id);

            if (account == null || account.UserId != userId)
                return null;

            account.BankName = request.BankName;
            account.AccountNumber = request.AccountNumber;
            account.AccountHolder = request.AccountHolder;
            account.AccountType = request.AccountType;

            var result = await _uow.BankAccounts.UpdateAsync(account);

            if (result == null)
                return null;

            return MapToResponse(result);
        }

        public async Task<bool> DeleteAsync(int id, int userId)
        {
            var account = await _uow.BankAccounts.GetByIdAsync(id);

            if (account == null || account.UserId != userId)
                return false;

            return await _uow.BankAccounts.DeleteAsync(id);
        }

        private static BankAccountResponse MapToResponse(BankAccount account)
        {
            return new BankAccountResponse
            {
                BankAccountId = account.BankAccountId,
                UserId = account.UserId,
                BankName = account.BankName,
                AccountNumber = account.AccountNumber,
                AccountHolder = account.AccountHolder,
                AccountType = account.AccountType,
                IsDefault = account.IsDefault,
                CreatedAt = account.CreatedAt,
                UpdatedAt = account.UpdatedAt
            };
        }
    }
}