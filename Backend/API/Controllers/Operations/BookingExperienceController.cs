using Contract.Services.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.Operations;
namespace Badminton.API.Controllers;

[ApiController, Authorize, Route("api/v1/booking-experience")]
public sealed class BookingExperienceController(IBookingAmendmentService amendment, IBatchBookingService batch, IFrontDeskBookingService desk,
    IBookingCalendarService calendar, IBookingCaseService cases, IGroupAttendanceService personalization) : BaseApiController
{
    [HttpGet("calendar-courts"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> CalendarCourts(int? branchId) => Resp(await calendar.CourtsAsync(branchId));
    [HttpGet("desk-customer"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> FindCustomer(string phone) => Resp(await desk.FindCustomerAsync(phone));
    [HttpPost("batch")]
    public async Task<IActionResult> Batch(BatchBookingRequest request) => Resp(await batch.CreateAsync(request));
    [HttpPost("desk"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Desk(DeskBookingRequest request) => Resp(await desk.CreateForCustomerAsync(request));
    [HttpPost("{id:int}/reschedule")]
    public async Task<IActionResult> Reschedule(int id, RescheduleRequest request)
    {
        await amendment.RescheduleAsync(id, request);
        return RespMsg("Đã đổi lịch.");
    }
    [HttpGet("calendar"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Calendar(DateOnly from, DateOnly to, int? branchId) => Resp(await calendar.CalendarAsync(from, to, branchId));
    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> History(int id) => Resp(await calendar.HistoryAsync(id));
    [HttpGet("{id:int}/calendar-file")]
    public async Task<IActionResult> CalendarFile(int id) => Resp(await calendar.ICalendarAsync(id));
    [HttpGet("alternatives")]
    public async Task<IActionResult> Alternatives(int courtId, DateOnly date, int slotId) => Resp(await calendar.AlternativesAsync(courtId, date, slotId));
    [HttpPost("{id:int}/cases")]
    public async Task<IActionResult> OpenCase(int id, BookingCaseRequest request) => Resp(await cases.CreateAsync(id, request));
    [HttpGet("cases")]
    public async Task<IActionResult> Cases(int? bookingId, int? branchId, int page = 1) => Resp(await cases.ListAsync(bookingId, branchId, page));
    [HttpPost("cases/{id:int}/resolve"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Resolve(int id, ResolutionRequest request)
    {
        await cases.ResolveAsync(id, request);
        return RespMsg("Đã xử lý yêu cầu.");
    }
    [HttpGet("{id:int}/rsvps")]
    public async Task<IActionResult> Rsvps(int id) => Resp(await personalization.RsvpsAsync(id));
    [HttpPost("{id:int}/invitations")]
    public async Task<IActionResult> Invite(int id, [FromBody] int[] userIds)
    {
        await personalization.InviteMembersAsync(id, userIds);
        return RespMsg("Đã gửi lời mời.");
    }
    [HttpPut("{id:int}/rsvp")]
    public async Task<IActionResult> Respond(int id, [FromBody] bool attending)
    {
        await personalization.RespondAsync(id, attending);
        return RespMsg("Đã lưu xác nhận.");
    }
}
