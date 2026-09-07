using Contract.Repositories.AuthEntity;

namespace Contract.Services.Interface
{
    public interface ITokenService
    {
        string GenerateToken(User user);
    }
}
