using Contract.Services.Staff;
using Badminton.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Badminton.API.Controllers.Admin
{
    /// <summary>
    /// Staff lifecycle — "manager &amp; admin do admin tạo" (docs 01 §2).
    /// Admin-only. Every mutation is audit-logged.
    /// </summary>
    [ApiController]
    [Route("api/v1/admin/staff")]
    [Authorize(Roles = "Admin")]
    public class StaffController : BaseApiController
    {
        private readonly IStaffService _svc;
        public StaffController(IStaffService svc) => _svc = svc;

        [HttpGet]
        public async Task<IActionResult> List() => Resp(await _svc.ListAsync());

        [HttpPost]
        public async Task<IActionResult> Create(StaffCreateRequest request)
            => Resp(await _svc.CreateAsync(request, CurrentUserId), "Tạo nhân viên thành công.");

        [HttpPost("{id:int}/lock")]
        public async Task<IActionResult> Lock(int id)
        {
            await _svc.SetStatusAsync(id, UserStatus.Locked, CurrentUserId);
            return RespMsg("Đã khóa tài khoản.");
        }

        [HttpPost("{id:int}/unlock")]
        public async Task<IActionResult> Unlock(int id)
        {
            await _svc.SetStatusAsync(id, UserStatus.Active, CurrentUserId);
            return RespMsg("Đã mở khóa tài khoản.");
        }

        [HttpPatch("{id:int}/role")]
        public async Task<IActionResult> ChangeRole(int id, [FromQuery] Role role)
        {
            await _svc.SetRoleAsync(id, role, CurrentUserId);
            return RespMsg($"Đã đổi role thành {role}.");
        }

        [HttpPatch("{id:int}/branch")]
        public async Task<IActionResult> AssignBranch(int id, [FromQuery] int branchId)
        {
            await _svc.AssignBranchAsync(id, branchId, CurrentUserId);
            return RespMsg($"Đã gán cơ sở {branchId}.");
        }
    }
}
