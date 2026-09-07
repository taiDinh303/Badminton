using Contract.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using ModelViews.Auth;
using Microsoft.AspNetCore.Authorization;
using ModelViews.User;

namespace BadmintonBE.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            var result = await _authService.RegisterAsync(request);

            return Ok(result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var result = await _authService.LoginAsync(request);

            return Ok(result);
        }

        [HttpPut("{userId:int}/role")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateRole(
            int userId,
            [FromBody] UpdateUserRoleRequest request)
        {
            var result = await _authService.UpdateRoleAsync(
                userId,
                request.RoleId);

            if (!result)
            {
                return BadRequest(new
                {
                    message = "User not found or role is invalid."
                });
            }

            return Ok(new
            {
                message = "User role updated successfully."
            });
        }
    }
}
