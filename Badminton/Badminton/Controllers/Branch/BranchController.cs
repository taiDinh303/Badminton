using Contract.Services.Branch;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.Branch;

namespace BadmintonBE.API.Controllers
{
    [ApiController]
    [Route("api/v1/branches")]
    public class BranchController : BaseApiController
    {
        private readonly IBranchService _svc;
        public BranchController(IBranchService svc) => _svc = svc;

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll() => Resp(await _svc.GetAllAsync());

        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id) => Resp(await _svc.GetByIdAsync(id));

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(CreateBranchRequest request) =>
            Resp(await _svc.CreateAsync(request, CurrentUserId), "Tạo cơ sở thành công.");

        [HttpPatch("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, UpdateBranchRequest request)
        {
            await _svc.UpdateAsync(id, request, CurrentUserId);
            return RespMsg("Cập nhật cơ sở thành công.");
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            await _svc.DeleteAsync(id, CurrentUserId);
            return RespMsg("Vô hiệu cơ sở thành công.");
        }
    }
}
