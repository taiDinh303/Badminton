using Contract.Repositories.Entity;

namespace Contract.Services.Interface
{
    public interface ITokenService
    {
        string GenerateToken(User user);
    }
}
