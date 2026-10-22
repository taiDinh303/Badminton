using Contract.Services.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.Account;

namespace Badminton.API.Controllers
{
    [ApiController]
    [Route("api/v1/account")]
    [Authorize]
    public class AccountController : BaseApiController
    {
        private readonly IAccountService _svc;
        public AccountController(IAccountService svc) => _svc = svc;

        [HttpGet("profile")]
        public async Task<IActionResult> Profile() => Resp(await _svc.GetProfileAsync(CurrentUserId));

        [HttpPatch("profile")]
        public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request) => Resp(await _svc.UpdateProfileAsync(CurrentUserId, request));

        [HttpGet("points")]
        public async Task<IActionResult> Points() =>
            Resp(await _svc.GetPointsAsync(CurrentUserId));

        [HttpGet("vouchers")]
        public async Task<IActionResult> Vouchers() =>
            Resp(await _svc.GetMyVouchersAsync(CurrentUserId));

        [HttpGet("summary")]
        public async Task<IActionResult> Summary() =>
            Resp(await _svc.GetAccountSummaryAsync(CurrentUserId));
    }
}
