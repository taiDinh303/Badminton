using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Utils;
using System.Data.Common;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Booking;
using Contract.Services.Operations;
using Contract.Services.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Context;
using Xunit;

namespace Badminton.Tests;

public sealed class OperationsCompletionTests
{
    [Fact]
    public async Task Concurrent_refund_and_reschedule_do_not_cross_the_authorized_branch()
    {
        var pause = new RefundPause();
        await using var h = new TestHost(configure: services => services.AddSingleton<IInterceptor>(pause));
        var b = await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon);
        var otherCourt = await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Courts.Where(c => c.BranchId == h.BranchId2).Select(c => c.Id).SingleAsync());
        h.ActAs(h.Manager, Role.Manager);
        pause.Armed = true;
        var refund = h.RunScopeAsync(sp => sp.GetRequiredService<IPaymentService>().RefundAsync(b.Id, h.Manager));
        Task? move = null;
        try
        {
            await pause.RefundReached.Task.WaitAsync(TimeSpan.FromSeconds(15));
            h.ActAs(h.Alice, Role.Customer);
            move = h.RunScopeAsync(sp => sp.GetRequiredService<IBookingAmendmentService>().RescheduleAsync(b.Id, new(otherCourt, h.SlotNoon, h.FutureDate, "Đổi sang cơ sở khác trong lúc hoàn tiền")));
            await pause.MoveAttempted.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
        finally { pause.Resume.TrySetResult(); }
        Assert.Equal(b.PayableAmount, (await refund).RefundAmount);
        await Assert.ThrowsAsync<ConflictException>(() => move!);
        var final = await h.GetBookingAsync(b.Id);
        Assert.Equal(h.BranchId, final.BranchId);
        Assert.Equal(BookingStatus.Refunded, final.Status);
        Assert.Equal(1, await h.CountRefundRowsAsync(b.Id));
    }

    [Fact]
    public async Task Every_operations_contract_resolves_from_the_registered_graph()
    {
        await using var h = new TestHost(configure: services => services.AddSingleton<IMediaStore, UnusedMediaStore>());
        await h.RunScopeAsync(sp =>
        {
            var contracts = typeof(IOperationsAccess).Assembly.GetTypes().Where(t => t.IsInterface && t.Namespace == typeof(IOperationsAccess).Namespace);
            foreach (var contract in contracts) Assert.NotNull(sp.GetRequiredService(contract));
            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData(DayOfWeek.Saturday)]
    [InlineData(DayOfWeek.Monday)]
    public async Task Alternative_prices_match_the_authoritative_quote(DayOfWeek weekday)
    {
        await using var h = new TestHost();
        h.ActAs(h.Alice, Role.Customer);
        var day = h.FutureDate;
        while (day.DayOfWeek != weekday) day = day.AddDays(1);
        var alternatives = await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingCalendarService>().AlternativesAsync(h.Court1, day, h.SlotNoon));
        Assert.NotEmpty(alternatives);
        foreach (var choice in alternatives)
        {
            var quote = await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().QuoteAsync(new(choice.CourtId, day.ToString("yyyy-MM-dd"), choice.SlotId), h.Alice));
            Assert.Equal(quote.Payable, choice.Price);
        }
    }

    [Fact]
    public async Task A_tracked_old_branch_cannot_authorize_a_refund_after_rescheduling()
    {
        await using var h = new TestHost();
        var booking = await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon);
        var otherCourt = await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Courts.Where(c => c.BranchId == h.BranchId2).Select(c => c.Id).SingleAsync());
        await h.RunScopeAsync(async sp =>
        {
            var tracked = await sp.GetRequiredService<IUnitOfWork>().GetRepository<Contract.Repositories.Entity.Booking>().GetByIdAsync(booking.Id);
            Assert.Equal(h.BranchId, tracked!.BranchId);
            h.ActAs(h.Alice, Role.Customer);
            await h.RunScopeAsync(other => other.GetRequiredService<IBookingAmendmentService>().RescheduleAsync(booking.Id, new(otherCourt, h.SlotNoon, h.FutureDate, "Chuyển cơ sở theo yêu cầu khách")));
            h.ActAs(h.Manager, Role.Manager);
            await Assert.ThrowsAsync<ConflictException>(() => sp.GetRequiredService<IPaymentService>().RefundAsync(booking.Id, h.Manager));
        });
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<IUnitOfWork>().Bookings.ClaimRefundAsync(booking.Id, false, "Old scope", DateTime.UtcNow, h.BranchId)));
        Assert.Equal(0, await h.CountRefundRowsAsync(booking.Id));
        Assert.Equal(BookingStatus.Confirmed, (await h.GetBookingAsync(booking.Id)).Status);
    }

    [Fact]
    public async Task Desk_booking_rejects_staff_accounts_as_customer_targets()
    {
        await using var h = new TestHost();
        h.ActAs(h.Manager, Role.Manager);
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IFrontDeskBookingService>()
            .CreateForCustomerAsync(new(h.ManagerOtherBranch, h.Court1, h.SlotNoon, h.FutureDate, 1, "staff-target"))));
        Assert.Equal(0, await h.CountBookingsForSlotAsync(h.Court1, h.FutureDate, h.SlotNoon));
    }

    private sealed class UnusedMediaStore : IMediaStore
    {
        public Task WriteAsync(string key, byte[] bytes, CancellationToken ct) => throw new InvalidOperationException("No media IO in DI test.");
        public Task<byte[]> ReadAsync(string key, CancellationToken ct) => throw new InvalidOperationException("No media IO in DI test.");
        public Task DeleteAsync(string key, CancellationToken ct) => throw new InvalidOperationException("No media IO in DI test.");
    }

    private sealed class RefundPause : DbCommandInterceptor
    {
        public bool Armed;
        private object? transitionKey;
        public readonly TaskCompletionSource RefundReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource MoveAttempted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Resume = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (!Armed) return result;
            if (command.Parameters.Count > 0 && (command.CommandText.Contains("sp_getapplock") || command.CommandText.Contains("pg_advisory_xact_lock")))
            {
                var key = command.Parameters[0].Value;
                if (transitionKey is null) transitionKey = key;
                else if (Equals(key, transitionKey)) MoveAttempted.TrySetResult();
            }
            if (command.CommandText.StartsWith("UPDATE") && command.CommandText.Contains("CancelReason"))
            {
                RefundReached.TrySetResult();
                await Resume.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            }
            return result;
        }
    }
}
