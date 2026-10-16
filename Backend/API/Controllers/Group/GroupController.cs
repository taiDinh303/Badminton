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
        public async Task<IActionResult> Join(int id)
        {
            await _svc.JoinAsync(id, CurrentUserId);
            return RespMsg("Tham gia nhóm thành công.");
        }

        [HttpGet("{id:int}/members")]
        public async Task<IActionResult> Members(int id) =>
            Resp(await _svc.GetMembersAsync(id));
    }
}
