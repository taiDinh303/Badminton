using Contract.Services.TimeSlot;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.TimeSlot;

namespace Badminton.API.Controllers
{
    [ApiController]
    [Route("api/v1/time-slots")]
    public class TimeSlotController : BaseApiController
    {
        private readonly ITimeSlotService _svc;
        public TimeSlotController(ITimeSlotService svc) => _svc = svc;

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
        {
            if (includeInactive && !User.IsInRole("Admin")) return Forbid();
            return Resp(await _svc.GetAllAsync(includeInactive));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(CreateTimeSlotRequest request) =>
            Resp(await _svc.CreateAsync(request, CurrentUserId), "Tạo khung giờ thành công.");

        [HttpPatch("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, UpdateTimeSlotRequest request)
        {
            await _svc.UpdateAsync(id, request, CurrentUserId);
            return RespMsg("Cập nhật khung giờ thành công.");
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            await _svc.DeleteAsync(id, CurrentUserId);
            return RespMsg("Vô hiệu khung giờ thành công.");
        }
    }
}
