using Contract.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.Auth;

namespace BadmintonBE.API.Controllers
{
    /// <summary>
    /// AUTH endpoints. Register/Login are anonymous; the rest of the API is
    /// JWT-protected. This controller only orchestrates: it calls the service
    /// and lets the <c>ExceptionMiddleware</c> shape any error response.
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    public class AuthController : BaseApiController
    {
        private readonly IAuthService _auth;

        public AuthController(IAuthService auth)
        {
            _auth = auth;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register(RegisterRequest request) =>
            Resp(await _auth.RegisterAsync(request), "Registered successfully.");

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginRequest request) =>
            Resp(await _auth.LoginAsync(request), "Login successful.");
    }
}
