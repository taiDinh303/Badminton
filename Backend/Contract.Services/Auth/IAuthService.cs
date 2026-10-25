using ModelViews.Auth;

namespace Contract.Services.Auth
{
    public interface IAuthService
    {
        Task<LoginResponse> RegisterAsync(RegisterRequest request);
        Task<LoginResponse> LoginAsync(LoginRequest request);
        Task ChangePasswordAsync(ChangePasswordRequest request);
        Task RevokeSessionsAsync();
        Task<RecoveryCodeResponse> IssueRecoveryCodeAsync(int userId, RecoveryCodeRequest request);
        Task ResetPasswordAsync(ResetPasswordRequest request);
    }
}
