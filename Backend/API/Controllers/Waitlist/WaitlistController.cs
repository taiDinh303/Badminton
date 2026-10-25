using Contract.Services.Waitlist;
using Badminton.Core.Base;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Badminton.API.Controllers
{
    [ApiController]
    [Route("api/v1/waitlist")]
    [Authorize]
    public class WaitlistController : BaseApiController
    {
        private readonly IWaitlistService _svc;
        public WaitlistController(IWaitlistService svc) => _svc = svc;

        [HttpPost("join")]
        public async Task<IActionResult> Join([FromBody] JoinWaitlistRequest req)
            => Resp(await _svc.JoinAsync(CurrentUserId, req.CourtId, req.SlotId,
                req.Date, req.PlayerCount, req.Note), "Đã thêm vào danh sách chờ.");

        [HttpPost("{id:int}/leave")]
        public async Task<IActionResult> Leave(int id)
            => Resp(await _svc.LeaveAsync(id, CurrentUserId), "Đã rời danh sách chờ.");

        [HttpGet("mine/paged")]
        public async Task<IActionResult> MinePaged([FromQuery] int page = 1)
        {
            var paging = new Contract.Repositories.UnitOfWork.Page(Math.Clamp(page, 1, 100000), 20);
            var (items, total) = await _svc.GetMinePagedAsync(CurrentUserId, paging);
            return Resp(new BasePaginatedList<WaitlistEntryView>(items, total, paging.Number, paging.Size));
        }

        [HttpGet("mine")]
        public async Task<IActionResult> Mine()
            => Resp(await _svc.GetMineAsync(CurrentUserId));
    }

    public sealed record JoinWaitlistRequest(
        [Range(1, int.MaxValue)] int CourtId,
        [Range(1, int.MaxValue)] int SlotId,
        [Required] DateOnly Date,
        int PlayerCount = 1,
        string? Note = null);
}
