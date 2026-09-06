using Contract.Repositories.Entity;
using Contract.Repositories.IOUW;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;

namespace Repositories.UOW
{
    public class UserRepository : IUserRepository
    {
        private readonly BadmintonBookingDbContext _context;

        public UserRepository(BadmintonBookingDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users
                .Include(x => x.Role)
                .FirstOrDefaultAsync(x => x.Email == email);
        }

        public async Task<bool> ExistsByEmailAsync(string email)
        {
            return await _context.Users
                .AnyAsync(x => x.Email == email);
        }

        public async Task<User> CreateAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user;
        }

        public async Task<User?> GetByIdAsync(int userId)
        {
            return await _context.Users
                .FirstOrDefaultAsync(x => x.UserId == userId);
        }
    }
}
