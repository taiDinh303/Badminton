using Contract.Services.Report;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Badminton.API.Controllers.Admin
{
    [ApiController]
    [Route("api/v1/admin")]
    [Authorize(Roles = "Admin,Manager")]
    public class ReportController : BaseApiController
    {
        private readonly IReportService _svc;
        public ReportController(IReportService svc) => _svc = svc;

        [HttpGet("report/summary")]
        public async Task<IActionResult> Summary([FromQuery] int? branchId = null)
            => Resp(await _svc.GetSummaryAsync(branchId));

        [HttpGet("report/revenue")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Revenue([FromQuery] DateOnly from,
            [FromQuery] DateOnly to,
            [FromQuery] int? branchId = null)
            => Resp(await _svc.GetRevenueAsync(from, to, branchId));

        [HttpGet("report/occupancy")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Occupancy([FromQuery] DateOnly from,
            [FromQuery] DateOnly to,
            [FromQuery] int? branchId = null)
            => Resp(await _svc.GetOccupancyAsync(from, to, branchId));
    }
}
