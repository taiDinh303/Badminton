using Contract.Services.Court;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.Court;

namespace BadmintonBE.API.Controllers
{
    [ApiController]
    [Route("api/v1/courts")]
    public class CourtController : BaseApiController
    {
        private readonly ICourtService _svc;
        public CourtController(ICourtService svc) => _svc = svc;

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll([FromQuery] int? branchId) =>
            Resp(await _svc.GetAllAsync(branchId));

        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id) => Resp(await _svc.GetByIdAsync(id));

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(CreateCourtRequest request) =>
            Resp(await _svc.CreateAsync(request, CurrentUserId), "Tạo sân thành công.");

        [HttpPatch("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, UpdateCourtRequest request)
        {
            await _svc.UpdateAsync(id, request, CurrentUserId);
            return RespMsg("Cập nhật sân thành công.");
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            await _svc.DeleteAsync(id, CurrentUserId);
            return RespMsg("Vô hiệu sân thành công.");
        }
    }
}
