using System.Collections.Concurrent;
using System.Text;
using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Utils;
using Contract.Repositories.Entity;
using Contract.Services.Operations;
using Contract.Services.Review;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelViews.Operations;
using ModelViews.Review;
using Repositories.Context;
using Xunit;

namespace Badminton.Tests;

public sealed class BookingExperienceTests
{
    private static TestHost Host() => new(configure: s =>
    {
        s.AddSingleton<IMediaStore, MemoryStore>();
        s.AddScoped<IReviewService, Services.ReviewService>();
    });
    private static async Task<int> Admin(TestHost h) => await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Users.Where(u => u.Role == Role.Admin).Select(u => u.Id).SingleAsync());
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j2ioAAAAASUVORK5CYII=");

    [Fact]
    public async Task Batch_replay_is_manifest_bound_and_any_conflict_rolls_every_slot_back()
    {
        await using var h = Host();
        h.ActAs(h.Alice, Role.Customer);
        var slot = await h.RunScopeAsync(async sp => { var db = sp.GetRequiredService<BadmintonBookingDbContext>(); var s = new TimeSlot { Label = "11:00", StartTime = new(11, 0), EndTime = new(12, 0), IsActive = true }; db.TimeSlots.Add(s); await db.SaveChangesAsync(); return s.Id; });
        var request = new BatchBookingRequest(h.Court1, h.FutureDate, [h.SlotMorning, slot], 2, 1, null, "manifest-a");
        var rows = await h.RunScopeAsync(sp => sp.GetRequiredService<IBatchBookingService>().CreateAsync(request));
        Assert.Equal(4, rows.Count);
        var replay = await h.RunScopeAsync(sp => sp.GetRequiredService<IBatchBookingService>().CreateAsync(request));
        Assert.Equal(rows.Select(b => b.Id), replay.Select(b => b.Id));
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IBatchBookingService>().CreateAsync(request with { Weeks = 1 })));
        await h.BookAsync(h.Bob, h.Court2, slot, h.FutureDate.ToString("yyyy-MM-dd"));
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IBatchBookingService>().CreateAsync(request with { CourtId = h.Court2, Weeks = 1, RequestKey = "rollback-b" })));
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings.CountAsync(b => b.UserId == h.Alice && b.CourtId == h.Court2)));
    }

    [Fact]
    public async Task Rescheduling_keeps_money_and_rejects_price_owner_and_attendance_changes()
    {
        await using var h = Host();
        var b = await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon);
        h.ActAs(h.Bob, Role.Customer);
        var request = new RescheduleRequest(h.Court2, h.SlotNoon, h.FutureDate, "Khách cần đổi sang sân bên cạnh");
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IBookingAmendmentService>().RescheduleAsync(b.Id, request)));
        h.ActAs(h.Alice, Role.Customer);
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IBookingAmendmentService>().RescheduleAsync(b.Id, request with { SlotId = h.SlotPeak })));
        await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingAmendmentService>().RescheduleAsync(b.Id, request));
        var changed = await h.GetBookingAsync(b.Id);
        Assert.Equal(h.Court2, changed.CourtId);
        Assert.Equal(b.PayableAmount, changed.PayableAmount);
        Assert.Single(await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingCalendarService>().HistoryAsync(b.Id)), x => x.Event == "rescheduled");
        await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings.Where(x => x.Id == b.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CheckedInAt, DateTime.UtcNow)));
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IBookingAmendmentService>().RescheduleAsync(b.Id, request with { CourtId = h.Court1 })));
    }

    [Fact]
    public async Task Desk_booking_and_calendar_enforce_branch_scope_and_preserve_customer_ownership()
    {
        await using var h = Host();
        h.ActAs(h.Manager, Role.Manager);
        var customer = await h.RunScopeAsync(sp => sp.GetRequiredService<IFrontDeskBookingService>().FindCustomerAsync("0900000001"));
        Assert.Equal(h.Alice, customer!.Id);
        var booking = await h.RunScopeAsync(sp => sp.GetRequiredService<IFrontDeskBookingService>().CreateForCustomerAsync(new(h.Alice, h.Court1, h.SlotNoon, h.FutureDate, 1, "desk-a")));
        Assert.Equal(h.Alice, booking.UserId);
        Assert.Equal("Pending", booking.Status);
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IBookingCalendarService>().CalendarAsync(h.FutureDate, h.FutureDate, h.BranchId2)));
        h.ActAs(h.ManagerOtherBranch, Role.Manager);
        Assert.Empty(await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingCalendarService>().CalendarAsync(h.FutureDate, h.FutureDate, null)));
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IFrontDeskBookingService>().CreateForCustomerAsync(new(h.Alice, h.Court1, h.SlotMorning, h.FutureDate, 1, "desk-b"))));
    }

    [Fact]
    public async Task Inbox_is_idempotent_private_and_read_state_is_independent_of_delivery()
    {
        await using var h = Host();
        h.ActAs(h.Alice, Role.Customer);
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => h.RunScopeAsync(sp => sp.GetRequiredService<IInboxService>().PublishAsync(h.Alice, null, "same-event", "Kiểm thử", "Nội dung thông báo"))));
        var rows = await h.RunScopeAsync(sp => sp.GetRequiredService<IInboxService>().ListAsync(1));
        Assert.Single(rows);
        Assert.Equal(1, await h.RunScopeAsync(sp => sp.GetRequiredService<IInboxService>().UnreadCountAsync()));
        h.ActAs(h.Bob, Role.Customer);
        Assert.Empty(await h.RunScopeAsync(sp => sp.GetRequiredService<IInboxService>().ListAsync(1)));
        await Assert.ThrowsAsync<NotFoundException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IInboxService>().ReadAsync(rows[0].Id)));
        h.ActAs(h.Alice, Role.Customer);
        await h.RunScopeAsync(sp => sp.GetRequiredService<IInboxService>().ReadAsync(rows[0].Id));
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<IInboxService>().UnreadCountAsync()));
    }

    [Fact]
    public async Task Reminders_respect_opt_out_and_are_not_duplicated_across_ticks()
    {
        await using var h = Host();
        var b = await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon);
        h.ActAs(h.Alice, Role.Customer);
        HanoiTime.NowUtc = () => h.HanoiNowUtc.AddHours(2.5);
        await h.RunScopeAsync(sp => sp.GetRequiredService<IInboxService>().SetPreferencesAsync(new(false, 60, false, false, false)));
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<IReminderService>().RunAsync()));
        await h.RunScopeAsync(sp => sp.GetRequiredService<IInboxService>().SetPreferencesAsync(new(true, 60, false, false, false)));
        Assert.Equal(1, await h.RunScopeAsync(sp => sp.GetRequiredService<IReminderService>().RunAsync()));
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<IReminderService>().RunAsync()));
        Assert.Equal(1, await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Set<InboxMessage>().CountAsync(m => m.BookingId == b.Id && m.EventKey.StartsWith("reminder:"))));
    }

    [Fact]
    public async Task Incidents_scope_replay_and_contact_outcomes_have_durable_history()
    {
        await using var h = Host();
        var b = await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon);
        h.ActAs(h.Manager, Role.Manager);
        var request = new IncidentRequest(h.BranchId, h.Court1, h.FutureDate, null, "Sân cần xử lý sự cố đèn chiếu sáng", "incident-a");
        Assert.Equal(1, await h.RunScopeAsync(sp => sp.GetRequiredService<IDisruptionService>().AnnounceAsync(request)));
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<IDisruptionService>().AnnounceAsync(request)));
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IDisruptionService>().AnnounceAsync(request with { Message = "Nội dung thay đổi không được replay" })));
        var rows = await h.RunScopeAsync(sp => sp.GetRequiredService<IDisruptionService>().ListAsync(null, 1));
        Assert.Single(rows);
        h.ActAs(h.ManagerOtherBranch, Role.Manager);
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IDisruptionService>().RecordContactAsync(rows[0].Id, new("Đã gọi điện và xác nhận đổi lịch"))));
        h.ActAs(h.Manager, Role.Manager);
        await h.RunScopeAsync(sp => sp.GetRequiredService<IDisruptionService>().RecordContactAsync(rows[0].Id, new("Đã gọi điện và xác nhận phương án xử lý")));
        Assert.NotNull((await h.RunScopeAsync(sp => sp.GetRequiredService<IDisruptionService>().ListAsync(null, 1)))[0].ContactedAt);
        Assert.Equal(BookingStatus.Confirmed, (await h.GetBookingAsync(b.Id)).Status);
    }

    [Fact]
    public async Task Shifts_reject_overlap_and_handover_cannot_be_closed_twice()
    {
        await using var h = Host();
        h.ActAs(h.Manager, Role.Manager);
        var request = new ShiftRequest(h.BranchId, h.Manager, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(8), "Ca trực ngày mai");
        await h.RunScopeAsync(sp => sp.GetRequiredService<IStaffWorkService>().CreateShiftAsync(request));
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IStaffWorkService>().CreateShiftAsync(request)));
        var id = await h.RunScopeAsync(sp => sp.GetRequiredService<IStaffWorkService>().CreateHandoverAsync(new(h.BranchId, "Kiểm tra lại đèn sân số một trước ca sau")));
        await h.RunScopeAsync(sp => sp.GetRequiredService<IStaffWorkService>().CloseHandoverAsync(id, new("Đã kiểm tra và bàn giao đầy đủ")));
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IStaffWorkService>().CloseHandoverAsync(id, new("Không được ghi đè kết quả cũ"))));
    }

    [Fact]
    public async Task Moderation_rebuilds_ratings_and_replies_keep_original_review_and_audit()
    {
        await using var h = Host();
        var b = await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon);
        await h.CompleteAsPlayedAsync(b.Id, h.Manager);
        h.ActAs(h.Alice, Role.Customer);
        var review = await h.RunScopeAsync(sp => sp.GetRequiredService<IReviewService>().CreateAsync(new CreateReviewRequest(b.Id, 5, "Đánh giá gốc"), h.Alice));
        h.ActAs(h.Bob, Role.Customer);
        await h.RunScopeAsync(sp => sp.GetRequiredService<IReviewCommunityService>().ReportAsync(review.Id, new("Nội dung cần xác minh trước khi hiển thị")));
        h.ActAs(h.Manager, Role.Manager);
        await h.RunScopeAsync(sp => sp.GetRequiredService<IReviewCommunityService>().ReplyAsync(review.Id, new("Cơ sở xin ghi nhận phản hồi của bạn")));
        await h.RunScopeAsync(sp => sp.GetRequiredService<IReviewCommunityService>().ModerateAsync(review.Id, new(true, "Ẩn tạm sau khi xác minh báo cáo")));
        Assert.Empty(await h.RunScopeAsync(sp => sp.GetRequiredService<IReviewService>().GetByCourtAsync(h.Court1)));
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Courts.Where(c => c.Id == h.Court1).Select(c => c.RatingCount).SingleAsync()));
        await h.RunScopeAsync(sp => sp.GetRequiredService<IReviewCommunityService>().ModerateAsync(review.Id, new(false, "Đã xác minh và khôi phục nội dung")));
        Assert.Single(await h.RunScopeAsync(sp => sp.GetRequiredService<IReviewService>().GetByCourtAsync(h.Court1)));
        Assert.Equal("Đánh giá gốc", await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Reviews.Where(r => r.Id == review.Id).Select(r => r.Comment).SingleAsync()));
    }

    [Fact]
    public async Task Privacy_requests_are_private_admin_resolved_and_never_erase_ledger()
    {
        await using var h = Host();
        h.ActAs(h.Alice, Role.Customer);
        var id = await h.RunScopeAsync(sp => sp.GetRequiredService<IPrivacyRequestService>().CreateAsync(new("Deletion", "Tôi yêu cầu xem xét xóa dữ liệu cá nhân")));
        h.ActAs(h.Bob, Role.Customer);
        Assert.Empty(await h.RunScopeAsync(sp => sp.GetRequiredService<IPrivacyRequestService>().ListAsync(1, false)));
        h.ActAs(h.Manager, Role.Manager);
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IPrivacyRequestService>().ResolveAsync(id, new(true, "Nhân viên không được tự xử lý dữ liệu"))));
        h.ActAs(await Admin(h), Role.Admin);
        await h.RunScopeAsync(sp => sp.GetRequiredService<IPrivacyRequestService>().ResolveAsync(id, new(false, "Chưa đủ thông tin xác minh; cần khách bổ sung trực tiếp")));
        Assert.NotNull(await h.GetUserAsync(h.Alice));
    }

    [Fact]
    public async Task Media_rejects_html_and_cross_owner_reads_and_publishes_avatar_explicitly()
    {
        await using var h = Host();
        h.ActAs(h.Alice, Role.Customer);
        await Assert.ThrowsAsync<BadRequestException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IMediaService>().UploadAsync(Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>"), null, null, default)));
        var photo = await h.RunScopeAsync(sp => sp.GetRequiredService<IMediaService>().UploadAsync(Png, null, null, default));
        h.ActAs(h.Bob, Role.Customer);
        await Assert.ThrowsAsync<NotFoundException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IMediaService>().ReadAsync(photo.Id, default)));
        h.ActAs(h.Alice, Role.Customer);
        await h.RunScopeAsync(sp => sp.GetRequiredService<IMediaService>().SetAvatarAsync(photo.Id));
        h.ActAs(h.Bob, Role.Customer);
        Assert.Equal(Png, (await h.RunScopeAsync(sp => sp.GetRequiredService<IMediaService>().ReadAsync(photo.Id, default))).Bytes);
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IMediaService>().DeleteAsync(photo.Id, default)));
    }

    [Fact]
    public async Task Csv_export_neutralizes_formulas_and_calendar_does_not_leak_other_bookings()
    {
        await using var h = Host();
        var b = await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon);
        await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings.Where(x => x.Id == b.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.BookingCode, "=HYPERLINK(\"bad\")")));
        h.ActAs(h.Manager, Role.Manager);
        var csv = await h.RunScopeAsync(sp => sp.GetRequiredService<IOperationsReportService>().BookingCsvAsync(h.FutureDate, h.FutureDate, null));
        Assert.Contains("\"'=HYPERLINK", csv);
        h.ActAs(h.Bob, Role.Customer);
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IBookingCalendarService>().ICalendarAsync(b.Id)));
        h.ActAs(h.Alice, Role.Customer);
        var ics = await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingCalendarService>().ICalendarAsync(b.Id));
        Assert.Contains("DTSTART:" + h.FutureDate.ToString("yyyyMMdd") + "T050000Z", ics);
        Assert.Contains("BEGIN:VEVENT", ics);
    }

    [Fact]
    public async Task Accepted_no_show_case_corrects_presence_points_counter_once_and_requires_branch()
    {
        await using var h = Host();
        var b = await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon);
        HanoiTime.NowUtc = () => h.HanoiNowUtc.AddHours(4);
        await h.RunScopeAsync(sp => sp.GetRequiredService<Contract.Services.Booking.IBookingService>().MarkNoShowAsync(b.Id, h.Manager));
        await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings.Where(x => x.Id == b.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CancelledAt, h.HanoiNowUtc.AddHours(4))));
        h.ActAs(h.Alice, Role.Customer);
        var id = await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingCaseService>().CreateAsync(b.Id, new("NoShow", "Tôi đã đến sân và cần nhân viên kiểm tra lại")));
        h.ActAs(h.ManagerOtherBranch, Role.Manager);
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IBookingCaseService>().ResolveAsync(id, new(true, "Đã xác minh khách thực sự có chơi tại sân"))));
        h.ActAs(h.Manager, Role.Manager);
        await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingCaseService>().ResolveAsync(id, new(true, "Đã xác minh khách thực sự có chơi tại sân")));
        Assert.Equal(BookingStatus.Completed, (await h.GetBookingAsync(b.Id)).Status);
        Assert.Equal(0, (await h.GetUserAsync(h.Alice)).NoShowCount);
        Assert.Equal(1, await h.CountPointRowsAsync(b.Id));
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IBookingCaseService>().ResolveAsync(id, new(true, "Không được xử lý lại để nhận thêm điểm"))));
        Assert.Equal(1, await h.CountPointRowsAsync(b.Id));
    }

    [Fact]
    public async Task Rsvp_counts_creator_capacity_and_revokes_access_when_member_leaves()
    {
        await using var h = Host();
        h.ActAs(h.Alice, Role.Customer);
        var group = await h.RunScopeAsync(sp => sp.GetRequiredService<Contract.Services.Group.IGroupService>().CreateAsync("Nhóm chơi kiểm thử", h.Alice));
        await h.RunScopeAsync(sp => sp.GetRequiredService<Contract.Services.Group.IGroupService>().JoinAsync(group.Id, h.Bob, group.InviteCode));
        var booking = await h.RunScopeAsync(sp => sp.GetRequiredService<Contract.Services.Booking.IBookingService>().CreateAsync(new(h.Court1, h.FutureDate.ToString("yyyy-MM-dd"), h.SlotNoon, 1, group.Id), h.Alice));
        await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupAttendanceService>().InviteMembersAsync(booking.Id, [h.Bob]));
        h.ActAs(h.Bob, Role.Customer);
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IGroupAttendanceService>().RespondAsync(booking.Id, true)));
        h.ActAs(h.Alice, Role.Customer);
        await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupAttendanceService>().RespondAsync(booking.Id, false));
        h.ActAs(h.Bob, Role.Customer);
        await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupAttendanceService>().RespondAsync(booking.Id, true));
        await h.RunScopeAsync(sp => sp.GetRequiredService<Contract.Services.Group.IGroupService>().LeaveAsync(group.Id, h.Bob));
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IGroupAttendanceService>().RespondAsync(booking.Id, true)));
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IGroupAttendanceService>().RsvpsAsync(booking.Id)));
    }

    public sealed class MemoryStore : IMediaStore
    {
        private readonly ConcurrentDictionary<string, byte[]> _files = new();
        public Task WriteAsync(string key, byte[] bytes, CancellationToken ct)
        {
            if (!_files.TryAdd(key, bytes))
                throw new IOException();
            return Task.CompletedTask;
        }
        public Task<byte[]> ReadAsync(string key, CancellationToken ct) => Task.FromResult(_files[key]);
        public Task DeleteAsync(string key, CancellationToken ct)
        {
            _files.TryRemove(key, out _);
            return Task.CompletedTask;
        }
    }
}
