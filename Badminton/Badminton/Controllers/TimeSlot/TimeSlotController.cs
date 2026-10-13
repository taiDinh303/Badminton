using Contract.Services.TimeSlot;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.TimeSlot;

namespace BadmintonBE.API.Controllers
{
    /// <summary>TIME-SLOT endpoints: read the slot list, admin manages slots.</summary>
    [ApiController]
    [Route("api/time-slots")]
    public class TimeSlotController : BaseApiController
    {
        private readonly ITimeSlotService _svc;

        public TimeSlotController(ITimeSlotService svc) => _svc = svc;

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll() => Resp(await _svc.GetAllAsync());

        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Create(CreateTimeSlotRequest request)
        {
            await _svc.CreateAsync(request, CurrentUserName);
            return RespMsg("Time slot created.");
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Update(int id, UpdateTimeSlotRequest request)
        {
            await _svc.UpdateAsync(id, request, CurrentUserName);
            return RespMsg("Time slot updated.");
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            await _svc.DeleteAsync(id, CurrentUserName);
            return RespMsg("Time slot deleted.");
        }
    }
}
