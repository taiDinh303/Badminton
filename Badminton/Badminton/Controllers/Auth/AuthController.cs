using Contract.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ModelViews.Auth;

namespace BadmintonBE.API.Controllers
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
    }
}
