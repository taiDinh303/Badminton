using Contract.Repositories.Entity;
using Contract.Repositories.IOUW;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;

namespace Repositories.UOW
{
    public class VerificationCodeRepository : IVerificationCodeRepository
    {
        private readonly BadmintonBookingDbContext _context;

        public VerificationCodeRepository(
            BadmintonBookingDbContext context)
        {
            _context = context;
        }

        public async Task<VerificationCode?> GetLatestAsync(
            int userId,
            string type)
        {
            return await _context.VerificationCodes
                .Where(x =>
                    x.UserId == userId &&
                    x.Type == type &&
                    !x.IsUsed)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task CreateAsync(
            VerificationCode verificationCode)
        {
            await _context.VerificationCodes.AddAsync(verificationCode);
        }

        public async Task UpdateAsync(
            VerificationCode verificationCode)
        {
            _context.VerificationCodes.Update(verificationCode);
            await Task.CompletedTask;
        }
    }
}
