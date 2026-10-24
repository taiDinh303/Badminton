using System.Security.Claims;
using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Security;
using Badminton.Core.Store;
using Badminton.Core.Utils;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Booking;
using Contract.Services.Group;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelViews.Booking;
using Repositories.Context;
using Services;
using Xunit;

namespace Badminton.Tests;

public sealed class ProjectQualityTests
{
    [Fact]
    public async Task Failed_notifications_are_retried_after_restart_and_concurrent_replays_do_not_resend()
    {
        var channel = new FlakyChannel();
        await using var h = new TestHost(configure: services => {
            services.RemoveAll<Contract.Services.Notify.INotifyChannel>();
            services.AddSingleton<Contract.Services.Notify.INotifyChannel>(channel);
        });
        var booking = await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon);
        await h.RunScopeAsync(async sp => {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            var row = await db.Notifications.SingleAsync(n => n.BookingId == booking.Id);
            Assert.Equal(NotifyStatus.Failed, row.Status);
            row.UpdatedAt = DateTime.UtcNow.AddMinutes(-3);
            await db.SaveChangesAsync();
        });
        await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => h.RunScopeAsync(sp =>
            sp.GetRequiredService<Contract.Services.Notify.INotifyService>().RetryPendingAsync())));
        await h.RunScopeAsync(sp => sp.GetRequiredService<Contract.Services.Notify.INotifyService>().NotifyBookingConfirmedAsync(booking.Id));
        Assert.Equal(2, channel.Calls); // failed first attempt, then one successful delivery
        var rows = await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Notifications.Where(n => n.BookingId == booking.Id).ToListAsync());
        Assert.Single(rows);
        Assert.Equal(NotifyStatus.Delivered, rows[0].Status);
    }

    private sealed class FlakyChannel : Contract.Services.Notify.INotifyChannel
    {
        public string Name => "log";
        public bool IsConfigured => true;
        public int Calls;
        public Task SendAsync(User user, NotifyEvent evt, string body)
        {
            if (Interlocked.Increment(ref Calls) == 1) throw new HttpRequestException("Synthetic transient failure");
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData("not-a-phone", "StrongPassword1")]
    [InlineData("0901234567", "x")]
    [InlineData("0901234567", "😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀")]
    public async Task Invalid_registration_is_rejected_without_creating_a_user(string phone, string password)
    {
        await using var h = new TestHost();
        await Assert.ThrowsAsync<BadRequestException>(() => h.RunScopeAsync(sp => new AuthService(
            sp.GetRequiredService<IUnitOfWork>(), sp.GetRequiredService<ICurrentUser>(),
            new JwtGenerator(Options.Create(new JwtSettings { Key = new string('a', 64), Issuer = "test", Audience = "test", ExpireMinutes = 10 })),
            new LoginAttemptTracker()).RegisterAsync(new("Test User", phone, password))));
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Users.CountAsync(u => u.PhoneNumber == phone)));
    }

    [Fact]
    public async Task Overlapping_slots_are_rejected_but_adjacent_slots_are_allowed()
    {
        await using var h = new TestHost();
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => SlotService(sp)
            .CreateAsync(new("Overlap", "10:30", "11:30", false), h.Manager)));
        var id = await h.RunScopeAsync(sp => SlotService(sp).CreateAsync(new("Adjacent", "11:00", "12:00", false), h.Manager));
        await h.RunScopeAsync(sp => SlotService(sp).UpdateAsync(id, new(IsActive: false, Label: null), h.Manager));
        Assert.DoesNotContain(await h.RunScopeAsync(sp => SlotService(sp).GetAllAsync()), s => s.Id == id);
        Assert.Contains(await h.RunScopeAsync(sp => SlotService(sp).GetAllAsync(true)), s => s.Id == id && !s.IsActive);
        await h.RunScopeAsync(sp => SlotService(sp).UpdateAsync(id, new(IsActive: true, Label: null), h.Manager));
        Assert.Contains(await h.RunScopeAsync(sp => SlotService(sp).GetAllAsync()), s => s.Id == id);
    }

    [Fact]
    public async Task Legacy_overlapping_slot_ids_cannot_create_two_bookings_and_availability_is_locked()
    {
        await using var h = new TestHost();
        var id = await h.RunScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            var slot = new TimeSlot { Label = "Legacy", StartTime = new(10, 30), EndTime = new(11, 30) };
            db.TimeSlots.Add(slot);
            await db.SaveChangesAsync();
            return slot.Id;
        });
        await h.BookAsync(h.Alice, h.Court1, h.SlotMorning, h.FutureDate.ToString("yyyy-MM-dd"));
        await Assert.ThrowsAsync<ConflictException>(() => h.BookAsync(h.Bob, h.Court1, id, h.FutureDate.ToString("yyyy-MM-dd")));
        var available = await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().GetAvailabilityAsync(h.BranchId, h.FutureDate));
        Assert.True(available.Single(c => c.CourtId == h.Court1).Slots.Single(s => s.SlotId == id).IsLocked);
    }

    [Fact]
    public async Task Concurrent_group_joins_respect_capacity_and_replays_are_idempotent()
    {
        await using var h = new TestHost();
        var group = await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().CreateAsync("Capacity", h.Alice, 2));
        async Task<bool> Join(int user)
        {
            try { await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().JoinAsync(group.Id, user)); return true; }
            catch (ConflictException) { return false; }
        }
        var results = await Task.WhenAll(Join(h.Bob), Join(h.Carol));
        Assert.Single(results, x => x);
        await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().JoinAsync(group.Id, h.Alice));
        Assert.Equal(2, await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Set<GroupMember>().CountAsync(m => m.GroupId == group.Id)));
    }

    [Fact]
    public async Task Quote_is_read_only_and_matches_creation_with_voucher_and_points()
    {
        await using var h = new TestHost();
        await h.AddVoucherAsync("QUOTE", 10, 1, 10000);
        await h.CountPointsIntoAsync(h.Alice, 1000, "quote fixture");
        var request = new CreateBookingRequest(h.Court1, h.FutureDate.ToString("yyyy-MM-dd"), h.SlotNoon,
            PlayerCount: 2, VoucherCode: "QUOTE", PointsToRedeem: 1000);
        var quote = await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().QuoteAsync(request, h.Alice));
        var solo = await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().QuoteAsync(request with { PlayerCount = 1 }, h.Alice));
        Assert.Equal(solo, quote);
        Assert.Equal(10000, quote.VoucherDiscount);
        Assert.Equal(20000, quote.PointsDiscount);
        Assert.Equal(0, (await h.GetVoucherAsync("QUOTE")).UsedQty);
        Assert.Equal(0, await h.CountBookingsForSlotAsync(h.Court1, h.FutureDate, h.SlotNoon));
        var booked = await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().CreateAsync(request, h.Alice));
        Assert.Equal(quote.Total, booked.TotalAmount);
        Assert.Equal(quote.Payable, booked.PayableAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task Player_count_outside_court_capacity_is_rejected(int players)
    {
        await using var h = new TestHost();
        var request = new CreateBookingRequest(h.Court1, h.FutureDate.ToString("yyyy-MM-dd"), h.SlotNoon, PlayerCount: players);
        await Assert.ThrowsAsync<BadRequestException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().QuoteAsync(request, h.Alice)));
        await Assert.ThrowsAsync<BadRequestException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().CreateAsync(request, h.Alice)));
    }

    [Fact]
    public async Task Reports_use_Hanoi_receipt_dates_and_include_refund_only_and_zero_days()
    {
        await using var h = new TestHost();
        var booking = await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon);
        var paidDay = h.FutureDate.AddDays(-2);
        await h.RunScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            var receipt = await db.Payments.SingleAsync(p => p.BookingId == booking.Id);
            receipt.PaidAt = DateTime.SpecifyKind(paidDay.ToDateTime(new TimeOnly(18, 0)), DateTimeKind.Utc); // next Hanoi day
            db.Payments.Add(new Payment { BookingId = booking.Id, UserId = h.Alice, Method = PaymentMethod.Cash,
                Amount = booking.PayableAmount, RefundAmount = 30000, Status = PaymentStatus.PartialRefund,
                RefundedAt = DateTime.SpecifyKind(h.FutureDate.ToDateTime(new TimeOnly(2, 0)), DateTimeKind.Utc) });
            await db.SaveChangesAsync();
        });
        var rows = await h.RunScopeAsync(sp => Report(sp, h.Manager).GetRevenueAsync(paidDay, h.FutureDate));
        Assert.Equal(3, rows.Count);
        Assert.Equal(0, rows[0].Revenue);
        Assert.Equal(booking.PayableAmount, rows[1].Revenue);
        Assert.Equal(0, rows[2].Revenue);
        Assert.Equal(30000, rows[2].Refunds);
        var summary = await h.RunScopeAsync(sp => Report(sp, h.Manager).GetSummaryAsync());
        Assert.Equal(-30000, summary.RevenueToday);
        var otherBranch = await h.RunScopeAsync(sp => Report(sp, h.ManagerOtherBranch).GetRevenueAsync(paidDay, h.FutureDate));
        Assert.All(otherBranch, row => Assert.Equal(0, row.Revenue + row.Refunds));
    }

    [Fact]
    public async Task Occupancy_excludes_closed_hours_and_maintenance_from_denominator()
    {
        await using var h = new TestHost();
        await h.RunScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            var branch = await db.Branches.FindAsync(h.BranchId);
            branch!.OpenTime = new(12, 0); branch.CloseTime = new(13, 0);
            db.Set<CourtDateStatusEntry>().Add(new() { CourtId = h.Court2, Date = h.FutureDate, Status = CourtDateStatus.Maintenance });
            await db.SaveChangesAsync();
        });
        var rows = await h.RunScopeAsync(sp => Report(sp, h.Manager).GetOccupancyAsync(h.FutureDate, h.FutureDate));
        Assert.Equal(1, rows[0].TotalSlots);
        Assert.Equal(0, rows[0].PeakTotal);
        Assert.Equal(0, rows[0].PeakFillRatePct);
    }

    private static TimeSlotService SlotService(IServiceProvider sp) => new(sp.GetRequiredService<IUnitOfWork>(),
        sp.GetRequiredService<ICurrentUser>(), sp.GetRequiredService<IAuditService>());

    private static ReportService Report(IServiceProvider sp, int actor)
    {
        var current = sp.GetRequiredService<TestHttpContextAccessor>();
        current.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, actor.ToString()), new Claim(ClaimTypes.Role, "Manager") }, "test")) };
        return new ReportService(sp.GetRequiredService<IUnitOfWork>(), current);
    }
}
