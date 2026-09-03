using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BadmintonBE.API.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UserController : ControllerBase
    {
        [Authorize]
        [HttpGet("me")]
        public IActionResult GetMe()
        {
            return Ok(new
            {
                message = "Bạn đã đăng nhập.",
                userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? User.FindFirstValue("sub"),
                email = User.FindFirstValue(ClaimTypes.Email),
                role = User.FindFirstValue(ClaimTypes.Role)
            });
        }
    }
}
