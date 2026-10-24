using ModelViews.Auth;

namespace Contract.Services.Auth
{
    public interface IAuthService
    {
        Task<LoginResponse> RegisterAsync(RegisterRequest request);
        Task<LoginResponse> LoginAsync(LoginRequest request);
        Task ChangePasswordAsync(ChangePasswordRequest request);
        Task RevokeSessionsAsync();
    }
}
