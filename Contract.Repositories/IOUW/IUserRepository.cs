using Contract.Repositories.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.Repositories.IOUW
{
    public interface IUserRepository
    {
        Task<User?> GetByEmailAsync(string email);

        Task<bool> ExistsByEmailAsync(string email);

        Task<User> CreateAsync(User user);

        Task<User?> GetByIdAsync(int userId);
    }
}
