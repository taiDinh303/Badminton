using Contract.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ModelViews.Auth;

namespace Badminton.API.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    [EnableRateLimiting("auth")]
    public class AuthController : BaseApiController
    {
        private readonly IAuthService _auth;
        public AuthController(IAuthService auth) => _auth = auth;

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register(RegisterRequest request) =>
            Resp(await _auth.RegisterAsync(request), "Đăng ký thành công.");

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginRequest request) =>
            Resp(await _auth.LoginAsync(request), "Đăng nhập thành công.");
        [HttpPost("recovery-code/{userId:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> IssueRecovery(int userId, RecoveryCodeRequest request) => Resp(await _auth.IssueRecoveryCodeAsync(userId, request));

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest request) { await _auth.ResetPasswordAsync(request); return Resp("Đã đặt lại mật khẩu. Đăng nhập bằng mật khẩu mới."); }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
        {
            await _auth.ChangePasswordAsync(request);
            return Resp("Đã đổi mật khẩu và thu hồi mọi phiên đăng nhập.");
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _auth.RevokeSessionsAsync();
            return Resp("Đã thu hồi mọi phiên đăng nhập.");
        }

    }
}
