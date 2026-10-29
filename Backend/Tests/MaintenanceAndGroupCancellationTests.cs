using System.Reflection;
using Badminton.Core.Base;
using Badminton.Core.Enums;
using Contract.Repositories.Entity;
using Contract.Services.Booking;
using Contract.Services.Group;
using Contract.Services.Maintenance;
using Contract.Services.Operations;
using Contract.Services.Points;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Context;
using Services;
using Xunit;

namespace Badminton.Tests;

public sealed class MaintenanceAndGroupCancellationTests
{
    [Theory]
    [InlineData(false, "court")]
    [InlineData(true, "court")]
    [InlineData(false, "date")]
    [InlineData(true, "date")]
    [InlineData(false, "slot")]
    [InlineData(true, "slot")]
    [InlineData(false, "reopen")]
    [InlineData(true, "reopen")]
    [InlineData(false, "narrow")]
    [InlineData(true, "narrow")]
    public async Task Stale_maintenance_work_does_not_cancel_or_refund_an_unaffected_booking(bool paid, string change)
    {
        var gate = new MaintenancePause();
        await using var h = new TestHost(configure: services => services.AddScoped<IBookingService>(sp => {
            var proxy = DispatchProxy.Create<IBookingService, PausedBookingService>();
            ((PausedBookingService)(object)proxy).Inner = ActivatorUtilities.CreateInstance<BookingService>(sp);
            ((PausedBookingService)(object)proxy).Gate = gate;
            return proxy;
        }));
        var b = paid ? await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon)
            : await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"));
        h.ActAs(h.Manager, Role.Manager);
        var closure = h.RunScopeAsync(sp => sp.GetRequiredService<IMaintenanceService>().SetAsync(h.Court1, h.FutureDate, CourtDateStatus.Maintenance, h.Manager,
            startTime: change == "slot" ? TimeSpan.FromHours(12) : null, endTime: change == "slot" ? TimeSpan.FromHours(14) : null));
        try
        {
            await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(20));
            if (change is "reopen" or "narrow")
                await h.RunScopeAsync(sp => sp.GetRequiredService<IMaintenanceService>().SetAsync(h.Court1, h.FutureDate,
                    change == "reopen" ? CourtDateStatus.Open : CourtDateStatus.Maintenance, h.Manager,
                    startTime: change == "narrow" ? TimeSpan.FromHours(10) : null,
                    endTime: change == "narrow" ? TimeSpan.FromHours(11) : null));
            else
            {
                h.ActAs(h.Alice, Role.Customer);
                var nextDay = Enumerable.Range(1, 7).Select(n => h.FutureDate.AddDays(n)).First(d => Weekend(d) == Weekend(h.FutureDate));
                await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingAmendmentService>().RescheduleAsync(b.Id,
                    new(change == "court" ? h.Court2 : h.Court1, change == "slot" ? h.SlotMorning : h.SlotNoon,
                        change == "date" ? nextDay : h.FutureDate, "Đổi lịch sang phạm vi không bảo trì")));
            }
            h.ActAs(h.Manager, Role.Manager);
        }
        finally { gate.Resume.TrySetResult(); }
        await closure;
        var final = await h.GetBookingAsync(b.Id);
        Assert.Equal(paid ? BookingStatus.Confirmed : BookingStatus.Pending, final.Status);
        Assert.Equal(b.PayableAmount, final.PayableAmount);
        Assert.Equal(0, await h.CountRefundRowsAsync(b.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Maintenance_still_processes_bookings_inside_the_closed_scope(bool paid)
    {
        await using var h = new TestHost();
        var b = paid ? await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon)
            : await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"));
        h.ActAs(h.Manager, Role.Manager);
        await h.RunScopeAsync(sp => sp.GetRequiredService<IMaintenanceService>().SetAsync(h.Court1, h.FutureDate, CourtDateStatus.Maintenance, h.Manager));
        Assert.Equal(paid ? BookingStatus.Refunded : BookingStatus.Cancelled, (await h.GetBookingAsync(b.Id)).Status);
        Assert.Equal(paid ? 1 : 0, await h.CountRefundRowsAsync(b.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Current_group_owner_cancels_old_bookings_but_benefits_remain_with_the_original_booker(bool paid)
    {
        await using var h = new TestHost();
        h.ActAs(h.Alice, Role.Customer);
        var group = await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().CreateAsync("Transfer cancellation", h.Alice));
        await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().JoinAsync(group.Id, h.Bob, group.InviteCode));
        await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().JoinAsync(group.Id, h.Carol, group.InviteCode));
        await h.CountPointsIntoAsync(h.Alice, 2000, "Original booker balance");
        await h.AddVoucherAsync("GROUP-CANCEL", 5, 1, 10000);
        var b = await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().CreateAsync(new(h.Court1,
            h.FutureDate.ToString("yyyy-MM-dd"), h.SlotNoon, GroupId: group.Id, VoucherCode: "GROUP-CANCEL", PointsToRedeem: 1000), h.Alice));
        if (paid) await h.ConfirmOnSiteAsync(b.Id, h.Manager);
        h.ActAs(h.Alice, Role.Customer);
        await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().TransferAsync(group.Id, h.Bob, h.Alice));
        h.ActAs(h.Carol, Role.Customer);
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().CancelAsync(b.Id, h.Carol)));
        h.ActAs(h.Bob, Role.Customer);
        Assert.Equal("Cancelled", (await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().CancelAsync(b.Id, h.Bob))).Status);
        Assert.Equal(h.Alice, (await h.GetBookingAsync(b.Id)).UserId);
        Assert.Equal(2000, await h.RunScopeAsync(sp => sp.GetRequiredService<IPointService>().GetBalanceAsync(h.Alice)));
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<IPointService>().GetBalanceAsync(h.Bob)));
        Assert.Equal(0, (await h.GetVoucherAsync("GROUP-CANCEL")).UsedQty);
        await h.RunScopeAsync(async sp => {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            if (paid) Assert.Equal(h.Alice, (await db.Payments.SingleAsync(p => p.BookingId == b.Id && p.Status == PaymentStatus.Refunded)).UserId);
            Assert.True(await db.AuditLogs.AnyAsync(a => a.ActorId == h.Bob && a.Action == (paid ? "booking.cancel-refund" : "booking.cancel")));
        });
    }

    private static bool Weekend(DateOnly d) => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_previous_group_owner_loses_delegated_cancellation_but_the_original_booker_retains_it(bool paid)
    {
        await using var h = new TestHost();
        h.ActAs(h.Alice, Role.Customer);
        var group = await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().CreateAsync("Multiple transfers", h.Alice));
        await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().JoinAsync(group.Id, h.Bob, group.InviteCode));
        await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().JoinAsync(group.Id, h.Carol, group.InviteCode));
        var b = await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().CreateAsync(new(h.Court1,
            h.FutureDate.ToString("yyyy-MM-dd"), h.SlotNoon, GroupId: group.Id), h.Alice));
        if (paid) await h.ConfirmOnSiteAsync(b.Id, h.Manager);
        h.ActAs(h.Alice, Role.Customer);
        await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().TransferAsync(group.Id, h.Bob, h.Alice));
        await h.RunScopeAsync(async sp => {
            await sp.GetRequiredService<BadmintonBookingDbContext>().PlayGroups.FindAsync(group.Id);
            h.ActAs(h.Bob, Role.Customer);
            await h.RunScopeAsync(other => other.GetRequiredService<IGroupService>().TransferAsync(group.Id, h.Carol, h.Bob));
            await Assert.ThrowsAsync<ConflictException>(() => sp.GetRequiredService<IBookingService>().CancelAsync(b.Id, h.Bob));
        });
        h.ActAs(h.Alice, Role.Customer);
        Assert.Equal("Cancelled", (await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().CancelAsync(b.Id, h.Alice))).Status);
    }

    public sealed class MaintenancePause
    {
        public readonly TaskCompletionSource Reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Resume = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public class PausedBookingService : DispatchProxy
    {
        public IBookingService Inner = null!;
        public MaintenancePause Gate = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == nameof(IBookingService.CancelForMaintenanceAsync)) return PauseAsync(method, args);
            try { return method.Invoke(Inner, args); }
            catch (TargetInvocationException e) when (e.InnerException is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }
        private async Task PauseAsync(MethodInfo method, object?[]? args)
        {
            Gate.Reached.TrySetResult();
            await Gate.Resume.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await (Task)method.Invoke(Inner, args)!;
        }
    }
}
