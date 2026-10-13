namespace Contract.Services.User
{
    public interface IUserRepository
    {
        Task<Contract.Repositories.Entity.User?> GetByEmailAsync(string email);

        Task<Contract.Repositories.Entity.User?> GetByIdAsync(int userId);

        Task<bool> ExistsByEmailAsync(string email);

        Task<Contract.Repositories.Entity.User> CreateAsync(Contract.Repositories.Entity.User user);
    }
}
