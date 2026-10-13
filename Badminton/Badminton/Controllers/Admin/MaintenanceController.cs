using Contract.Services.Maintenance;
using M.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BadmintonBE.API.Controllers
{
    [ApiController]
    [Route("api/v1/admin")]
    [Authorize(Roles = "Admin,Manager")]
    public class MaintenanceController : BaseApiController
    {
        private readonly IMaintenanceService _svc;
        public MaintenanceController(IMaintenanceService svc) => _svc = svc;

        [HttpPost("courts/{courtId:int}/maintenance")]
        public async Task<IActionResult> Set(int courtId, [FromQuery] string date,
            [FromQuery] CourtDateStatus status, [FromQuery] string? note = null)
        {
            var d = DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture);
            await _svc.SetAsync(courtId, d, status, CurrentUserId, note);
            return RespMsg("Đã cập nhật trạng thái bảo trì.");
        }

        [HttpGet("courts/{courtId:int}/maintenance/{date}")]
        public async Task<IActionResult> Get(int courtId, [FromRoute] string date)
        {
            var d = DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture);
            return Resp(await _svc.GetStatusAsync(courtId, d));
        }
    }
}
