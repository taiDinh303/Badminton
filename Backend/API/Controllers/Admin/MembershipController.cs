using Contract.Services.Membership;
using Badminton.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Badminton.API.Controllers.Admin
{
    [ApiController]
    [Route("api/v1/admin/membership")]
    [Authorize(Roles = "Admin")]
    public class MembershipController : BaseApiController
    {
        private readonly IMembershipService _svc;
        public MembershipController(IMembershipService svc) => _svc = svc;

        [HttpPost("assign")]
        public async Task<IActionResult> Assign([FromBody] AssignTierRequest req)
        {
            var updated = await _svc.AssignTierAsync(req.UserId, req.Tier, CurrentUserId);
            return Resp(new
            {
                updated.Id,
                updated.FullName,
                updated.Tier,
                Multiplier = _svc.EffectiveMultiplier(updated)
            }, "Đã gán membership thành công.");
        }

        [HttpGet("customers")]
        public async Task<IActionResult> Customers() => Resp(await _svc.GetMembersAsync(true));

        [HttpGet("members")]
        public async Task<IActionResult> Members()
            => Resp(await _svc.GetMembersAsync());
    }

    public sealed record AssignTierRequest(
        [Range(1, int.MaxValue)] int UserId,
        [Required] MembershipTier Tier);
}
