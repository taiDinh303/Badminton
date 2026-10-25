using Contract.Services.Group;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.Group;

namespace Badminton.API.Controllers
{
    [ApiController]
    [Route("api/v1/groups")]
    [Authorize]
    public class GroupController : BaseApiController
    {
        private readonly IGroupService _svc;
        public GroupController(IGroupService svc) => _svc = svc;

        [HttpGet("mine")]
        public async Task<IActionResult> Mine() => Resp(await _svc.GetByOwnerAsync(CurrentUserId));

        [HttpPost]
        public async Task<IActionResult> Create(CreateGroupRequest request) =>
            Resp(await _svc.CreateAsync(request.Name, CurrentUserId, request.MaxMembers),
                "Tạo nhóm thành công.");

        [HttpPost("{id:int}/join")]
        public async Task<IActionResult> Join(int id, [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] JoinGroupRequest? request = null)
        {
            await _svc.JoinAsync(id, CurrentUserId, request?.InviteCode);
            return RespMsg("Tham gia nhóm thành công.");
        }

        [HttpGet("joined")]
        public async Task<IActionResult> Joined() => Resp(await _svc.GetJoinedAsync(CurrentUserId));
        [HttpPost("{id:int}/leave")]
        public async Task<IActionResult> Leave(int id) { await _svc.LeaveAsync(id, CurrentUserId); return RespMsg("Đã rời nhóm."); }
        [HttpDelete("{id:int}/members/{userId:int}")]
        public async Task<IActionResult> Remove(int id, int userId) { await _svc.RemoveMemberAsync(id, userId, CurrentUserId); return RespMsg("Đã loại thành viên."); }
        [HttpPatch("{id:int}")]
        public async Task<IActionResult> Rename(int id, RenameGroupRequest request) { await _svc.RenameAsync(id, request.Name, CurrentUserId); return RespMsg("Đã đổi tên nhóm."); }
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Disband(int id) { await _svc.DisbandAsync(id, CurrentUserId); return RespMsg("Đã giải tán nhóm."); }
        [HttpPost("{id:int}/transfer")]
        public async Task<IActionResult> Transfer(int id, TransferGroupRequest request) { await _svc.TransferAsync(id, request.UserId, CurrentUserId); return RespMsg("Đã chuyển chủ nhóm và thu hồi mã mời cũ."); }
        [HttpPost("{id:int}/invite")]
        public async Task<IActionResult> Invite(int id) => Resp(new { Code = await _svc.RotateInviteAsync(id, CurrentUserId) });

        [HttpGet("{id:int}/members")]
        public async Task<IActionResult> Members(int id) =>
            Resp(await _svc.GetMembersAsync(id));
    }
}
