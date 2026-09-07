using Contract.Repositories.Entity;

namespace Contract.Repositories.IOUW
{
    public interface IUserRepository
    {
        Task<User?> GetByEmailAsync(string email);

        Task<bool> ExistsByEmailAsync(string email);

        Task<User> CreateAsync(User user);

        Task<User?> GetByIdAsync(int userId);

        Task<bool> UpdateRoleAsync(int userId, int roleId);
    }
}
