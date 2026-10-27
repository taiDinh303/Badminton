using Contract.Services.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.Operations;
namespace Badminton.API.Controllers;

[ApiController, Authorize(Roles = "Admin,Manager"), Route("api/v1/operations")]
public sealed class OperationsController(IDisruptionService disruptions, IStaffWorkService staff, IOperationsReportService reports) : BaseApiController
{
    [HttpGet("shift-staff")]
    public async Task<IActionResult> ShiftStaff(int? branchId) => Resp(await staff.StaffAsync(branchId));
    [HttpPost("incidents")]
    public async Task<IActionResult> Announce(IncidentRequest request) => Resp(await disruptions.AnnounceAsync(request));
    [HttpGet("incidents")]
    public async Task<IActionResult> Incidents(int? branchId, int page = 1) => Resp(await disruptions.ListAsync(branchId, page));
    [HttpPut("incidents/{id:int}/contact")]
    public async Task<IActionResult> Contact(int id, ContactOutcomeRequest request)
    {
        await disruptions.RecordContactAsync(id, request);
        return RespMsg("Đã ghi kết quả liên hệ.");
    }
    [HttpGet("shifts")]
    public async Task<IActionResult> Shifts(DateOnly from, DateOnly to, int? branchId) => Resp(await staff.ShiftsAsync(from, to, branchId));
    [HttpPost("shifts")]
    public async Task<IActionResult> Shift(ShiftRequest request) => Resp(await staff.CreateShiftAsync(request));
    [HttpDelete("shifts/{id:int}")]
    public async Task<IActionResult> DeleteShift(int id)
    {
        await staff.DeleteShiftAsync(id);
        return RespMsg("Đã xóa ca trực.");
    }
    [HttpGet("handover")]
    public async Task<IActionResult> Handover(int? branchId, int page = 1) => Resp(await staff.HandoverAsync(branchId, page));
    [HttpPost("handover")]
    public async Task<IActionResult> Handover(HandoverRequest request) => Resp(await staff.CreateHandoverAsync(request));
    [HttpPut("handover/{id:int}/close")]
    public async Task<IActionResult> CloseHandover(int id, ContactOutcomeRequest request)
    {
        await staff.CloseHandoverAsync(id, request);
        return RespMsg("Đã hoàn tất bàn giao.");
    }
    [HttpGet("booking-export")]
    public async Task<IActionResult> BookingExport(DateOnly from, DateOnly to, int? branchId) => Resp(await reports.BookingCsvAsync(from, to, branchId));
    [HttpGet("audit-export"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> AuditExport(DateOnly from, DateOnly to) => Resp(await reports.AuditCsvAsync(from, to));
    [HttpGet("insights")]
    public async Task<IActionResult> Insights(DateOnly from, DateOnly to, int? branchId) => Resp(await reports.InsightsAsync(from, to, branchId));
}
