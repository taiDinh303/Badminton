using Contract.Repositories.IOUW;
using Repositories.Context;

namespace Repositories.UOW
{
    public class UOW : IUOW
    {
        private readonly BadmintonBookingDbContext _context;

        public IUserRepository Users { get; }

        public UOW(
            BadmintonBookingDbContext context,
            IUserRepository users)
        {
            _context = context;
            Users = users;
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}
