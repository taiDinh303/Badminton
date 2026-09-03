using Contract.Repositories.Entity;

namespace Contract.Services.Interface
{
    public interface IUserRepository
    {
        Task<User?> GetByEmailAsync(string email);

        Task<User?> GetByIdAsync(int userId);

        Task<bool> ExistsByEmailAsync(string email);

        Task<User> CreateAsync(User user);
    }
}
