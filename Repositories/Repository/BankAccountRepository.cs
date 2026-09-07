using Contract.Repositories.Entity;
using Contract.Repositories.IOUW;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;

namespace Repositories.Repository
{
    public class BankAccountRepository : IBankAccountRepository
    {
        private readonly BadmintonBookingDbContext _context;

        public BankAccountRepository(BadmintonBookingDbContext context)
        {
            _context = context;
        }

        public async Task<List<BankAccount>> GetByUserIdAsync(int userId)
        {
            return await _context.BankAccounts
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.IsDefault)
                .ThenByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<BankAccount?> GetByIdAsync(int id)
        {
            return await _context.BankAccounts
                .FirstOrDefaultAsync(x => x.BankAccountId == id);
        }

        public async Task<BankAccount> CreateAsync(BankAccount bankAccount)
        {
            _context.BankAccounts.Add(bankAccount);
            await _context.SaveChangesAsync();

            return bankAccount;
        }

        public async Task<BankAccount?> UpdateAsync(BankAccount bankAccount)
        {
            var existing = await _context.BankAccounts
                .FirstOrDefaultAsync(x => x.BankAccountId == bankAccount.BankAccountId);

            if (existing == null)
                return null;

            existing.BankName = bankAccount.BankName;
            existing.AccountNumber = bankAccount.AccountNumber;
            existing.AccountHolder = bankAccount.AccountHolder;
            existing.AccountType = bankAccount.AccountType;
            existing.IsDefault = bankAccount.IsDefault;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return existing;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var bankAccount = await _context.BankAccounts
                .FirstOrDefaultAsync(x => x.BankAccountId == id);

            if (bankAccount == null)
                return false;

            _context.BankAccounts.Remove(bankAccount);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}