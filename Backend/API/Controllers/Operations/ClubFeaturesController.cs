using Contract.Services.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.Operations;
namespace Badminton.API.Controllers;

[ApiController, Authorize, Route("api/v1/club/players")]
public sealed class PlayerMatchingController(IPlayerMatchingService service) : BaseApiController
{
    [HttpGet] public async Task<IActionResult> List(int? branchId, string? area, int? level, DateTime fromUtc, DateTime toUtc, int page = 1) => Resp(await service.SearchAsync(branchId, area, level, fromUtc, toUtc, page));
    [HttpPost] public async Task<IActionResult> Create(PlayerSessionRequest r) => Resp(await service.CreateAsync(r));
    [HttpPost("{id:int}/join")]
    public async Task<IActionResult> Join(int id)
    {
        await service.RequestAsync(id);
        return RespMsg("Đã gửi yêu cầu tham gia.");
    }
    [HttpGet("{id:int}/members")] public async Task<IActionResult> Members(int id) => Resp(await service.MembersAsync(id));
    [HttpPut("{id:int}/members/{userId:int}")]
    public async Task<IActionResult> Decide(int id, int userId, [FromBody] bool accept)
    {
        await service.DecideAsync(id, userId, accept);
        return RespMsg("Đã lưu quyết định.");
    }
    [HttpPost("{id:int}/leave")]
    public async Task<IActionResult> Leave(int id)
    {
        await service.LeaveAsync(id);
        return RespMsg("Đã rút yêu cầu/tham gia.");
    }
    [HttpPost("{id:int}/close")]
    public async Task<IActionResult> Close(int id)
    {
        await service.CloseAsync(id);
        return RespMsg("Đã đóng buổi chơi.");
    }
}
[ApiController, Authorize, Route("api/v1/club/expenses")]
public sealed class GroupExpensesController(IGroupExpenseService service) : BaseApiController
{
    [HttpPost("{bookingId:int}/attendance")]
    public async Task<IActionResult> Attendance(int bookingId, AttendanceSplitRequest request) { await service.CreateByAttendanceAsync(bookingId, request); return RespMsg("Đã chia theo phút tham dự."); }
    [HttpGet("bookings")] public async Task<IActionResult> Bookings(int groupId, int page = 1) => Resp(await service.BookingsAsync(groupId, page));
    [HttpGet("{bookingId:int}")] public async Task<IActionResult> Get(int bookingId) => Resp(await service.GetAsync(bookingId));
    [HttpPost("{bookingId:int}")]
    public async Task<IActionResult> Create(int bookingId, [FromBody] int[] members)
    {
        await service.CreateAsync(bookingId, members);
        return RespMsg("Đã chốt bảng chia chi phí.");
    }
    [HttpPut("{bookingId:int}/members/{userId:int}")]
    public async Task<IActionResult> Record(int bookingId, int userId, [FromBody] bool paid)
    {
        await service.RecordAsync(bookingId, userId, paid);
        return RespMsg("Đã ghi nhận đóng góp nội bộ.");
    }
}
[ApiController, Authorize, Route("api/v1/club/equipment")]
public sealed class EquipmentController(IEquipmentService service) : BaseApiController
{
    [HttpGet("orders/{id:int}/condition")] public async Task<IActionResult> Condition(int id) => Resp(await service.ConditionAsync(id));
    [HttpPost("orders/{id:int}/condition"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Condition(int id, EquipmentConditionRequest request) { await service.RecordConditionAsync(id, request); return RespMsg("Đã chốt biên bản và điều chỉnh tồn kho. Khoản đối chiếu chưa phải khoản đã thu."); }
    [HttpGet] public async Task<IActionResult> Items(int branchId) => Resp(await service.ItemsAsync(branchId));
    [HttpPost, Authorize(Roles = "Admin,Manager")] public async Task<IActionResult> Create(EquipmentItemRequest r) => Resp(await service.SaveItemAsync(null, r));
    [HttpPut("{id:int}"), Authorize(Roles = "Admin,Manager")] public async Task<IActionResult> Save(int id, EquipmentItemRequest r) => Resp(await service.SaveItemAsync(id, r));
    [HttpPost("orders")] public async Task<IActionResult> Reserve(EquipmentOrderRequest r) => Resp(await service.ReserveAsync(r));
    [HttpGet("orders")] public async Task<IActionResult> Orders(bool staff = false, int? branchId = null, int page = 1) => Resp(await service.OrdersAsync(staff, branchId, page));
    [HttpPost("orders/{id:int}/{actionName}")]
    public async Task<IActionResult> Transition(int id, string actionName)
    {
        await service.TransitionAsync(id, actionName);
        return RespMsg("Đã cập nhật yêu cầu dụng cụ.");
    }
}
[ApiController, Authorize, Route("api/v1/club/coaches")]
public sealed class CoachingController(ICoachingService service) : BaseApiController
{
    [HttpGet("court-bookings"), Authorize(Roles = "Admin,Manager")] public async Task<IActionResult> Courts(int branchId) => Resp(await service.CourtChoicesAsync(branchId));
    [HttpPut("slots/{id:int}/court"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Attach(int id, [FromBody] int bookingId) { await service.AttachCourtAsync(id, bookingId); return RespMsg("Đã gắn sân cho lớp học."); }
    [HttpGet] public async Task<IActionResult> Coaches(int branchId) => Resp(await service.CoachesAsync(branchId));
    [HttpPost, Authorize(Roles = "Admin,Manager")] public async Task<IActionResult> Create(CoachRequest r) => Resp(await service.SaveCoachAsync(null, r));
    [HttpPut("{id:int}"), Authorize(Roles = "Admin,Manager")] public async Task<IActionResult> Save(int id, CoachRequest r) => Resp(await service.SaveCoachAsync(id, r));
    [HttpGet("{id:int}/slots")] public async Task<IActionResult> Slots(int id, DateTime fromUtc, DateTime toUtc) => Resp(await service.SlotsAsync(id, fromUtc, toUtc));
    [HttpPost("{id:int}/slots"), Authorize(Roles = "Admin,Manager")] public async Task<IActionResult> AddSlot(int id, CoachSlotRequest r) => Resp(await service.AddSlotAsync(id, r));
    [HttpPost("slots/{id:int}/cancel"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> CancelSlot(int id)
    {
        await service.CancelSlotAsync(id);
        return RespMsg("Đã hủy buổi học và các chỗ đã đặt.");
    }
    [HttpPost("reservations")] public async Task<IActionResult> Reserve(CoachingRequest r) => Resp(await service.ReserveAsync(r));
    [HttpGet("reservations")] public async Task<IActionResult> Reservations(bool staff = false, int? branchId = null, int page = 1) => Resp(await service.ReservationsAsync(staff, branchId, page));
    [HttpPost("reservations/{id:int}/{actionName}")]
    public async Task<IActionResult> Transition(int id, string actionName)
    {
        await service.TransitionAsync(id, actionName);
        return RespMsg("Đã cập nhật buổi học.");
    }
}
[ApiController, Authorize, Route("api/v1/club/tournaments")]
public sealed class TournamentsController(ITournamentService service) : BaseApiController
{
    [HttpGet("{id:int}/matches/{matchId:int}/history")] public async Task<IActionResult> History(int id, int matchId) => Resp(await service.ScoreHistoryAsync(id, matchId));
    [HttpGet] public async Task<IActionResult> List(int? branchId, int page = 1) => Resp(await service.ListAsync(branchId, page));
    [HttpPost, Authorize(Roles = "Admin,Manager")] public async Task<IActionResult> Create(TournamentRequest r) => Resp(await service.CreateAsync(r));
    [HttpGet("{id:int}")] public async Task<IActionResult> Detail(int id) => Resp(await service.DetailAsync(id));
    [HttpPost("{id:int}/teams")]
    public async Task<IActionResult> Register(int id, TeamRequest r)
    {
        await service.RegisterAsync(id, r);
        return RespMsg("Đã đăng ký đội.");
    }
    [HttpDelete("{id:int}/teams/mine")]
    public async Task<IActionResult> Withdraw(int id)
    {
        await service.WithdrawAsync(id);
        return RespMsg("Đã rút đội.");
    }
    [HttpPost("{id:int}/draw"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Draw(int id, [FromBody] int pools)
    {
        await service.DrawAsync(id, pools);
        return RespMsg("Đã chia bảng và tạo vòng đấu.");
    }
    [HttpPut("{id:int}/matches/{matchId:int}/schedule"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Schedule(int id, int matchId, MatchScheduleRequest r)
    {
        await service.ScheduleAsync(id, matchId, r);
        return RespMsg("Đã xếp lịch đấu.");
    }
    [HttpPut("{id:int}/matches/{matchId:int}/score"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Score(int id, int matchId, MatchScoreRequest r)
    {
        await service.ScoreAsync(id, matchId, r);
        return RespMsg("Đã ghi kết quả và cập nhật xếp hạng.");
    }
    [HttpPost("{id:int}/cancel"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Cancel(int id)
    {
        await service.CancelAsync(id);
        return RespMsg("Đã hủy giải.");
    }
}
[ApiController, Route("api/v1/club/calendar")]
public sealed class CalendarSubscriptionController(ICalendarSubscriptionService service) : BaseApiController
{
    [HttpGet, Authorize] public async Task<IActionResult> State() => Resp(await service.StateAsync());
    [HttpPost, Authorize]
    public async Task<IActionResult> Rotate()
    {
        Response.Headers.CacheControl = "no-store";
        return Resp(await service.RotateAsync());
    }
    [HttpDelete, Authorize]
    public async Task<IActionResult> Revoke()
    {
        await service.RevokeAsync();
        return RespMsg("Đã thu hồi liên kết lịch.");
    }
    [HttpGet("feed/{token}.ics"), AllowAnonymous, Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("public")]
    public async Task<IActionResult> Feed(string token)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return Content(await service.FeedAsync(token), "text/calendar; charset=utf-8");
    }
}
