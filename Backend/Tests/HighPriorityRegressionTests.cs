using Badminton.Core.Base;
using Badminton.Core.Enums;
using Contract.Repositories.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelViews.Court;
using Repositories.Context;
using Services;
using Xunit;

namespace Badminton.Tests;

public sealed class HighPriorityRegressionTests
{
    [Fact]
    public async Task Deleted_paid_booking_cannot_enter_the_refund_path()
    {
        await using var h = new TestHost();
        var booking = await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon);
        await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings.Where(b => b.Id == booking.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.DeletedAt, DateTime.UtcNow)));
        h.ActAs(h.Alice, Role.Customer);
        await Assert.ThrowsAsync<NotFoundException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<Contract.Services.Booking.IBookingService>().CancelAsync(booking.Id, h.Alice)));
        Assert.Equal(0, await h.CountRefundRowsAsync(booking.Id));
    }
    [Fact]
    public async Task Expired_or_missing_deadline_cannot_create_an_onsite_receipt()
    {
        await using var h = new TestHost();
        var booking = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"));
        foreach (var deadline in new DateTime?[] { DateTime.UtcNow.AddMinutes(-1), null })
        {
            await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings
                .Where(b => b.Id == booking.Id).ExecuteUpdateAsync(s => s.SetProperty(b => b.PendingExpiresAt, deadline)));
            await Assert.ThrowsAsync<ConflictException>(() => h.ConfirmOnSiteAsync(booking.Id, h.Manager));
            Assert.Equal(BookingStatus.Pending, (await h.GetBookingAsync(booking.Id)).Status);
            Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Payments.CountAsync()));
            Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Notifications.CountAsync()));
        }
    }

    [Fact]
    public async Task Conditional_confirmation_rechecks_expiry_even_after_a_caller_read_the_booking()
    {
        await using var h = new TestHost();
        var booking = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"));
        var deadline = DateTime.UtcNow.AddMinutes(1);
        await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings
            .Where(b => b.Id == booking.Id).ExecuteUpdateAsync(s => s.SetProperty(b => b.PendingExpiresAt, deadline)));
        // A stale caller can reach the repository after TTL elapsed. Equality is expired too.
        foreach (var now in new[] { deadline, deadline.AddTicks(1) })
            Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<IUnitOfWork>().Bookings.ConfirmAsync(booking.Id, "test-token", now)));
        Assert.Equal(BookingStatus.Pending, (await h.GetBookingAsync(booking.Id)).Status);
        Assert.Equal(1, await h.RunScopeAsync(sp => sp.GetRequiredService<IUnitOfWork>().Bookings.ConfirmAsync(booking.Id, "test-token", deadline.AddSeconds(-1))));
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<IUnitOfWork>().Bookings.ConfirmAsync(booking.Id, "duplicate", deadline.AddSeconds(-1))));
    }

    [Fact]
    public async Task Court_create_and_patch_reject_invalid_values_without_dirtying_the_tracked_entity()
    {
        await using var h = new TestHost();
        var invalid = new[]
        {
            new UpdateCourtRequest(null, -1, null, null, null, null, null, null),
            new UpdateCourtRequest(null, null, 0, null, null, null, null, null),
            new UpdateCourtRequest(null, null, null, -0.01m, null, null, null, null),
            new UpdateCourtRequest(null, null, null, null, 0, null, null, null),
            new UpdateCourtRequest(null, null, int.MaxValue, 0.2m, null, null, null, null),
            new UpdateCourtRequest(" ", null, null, null, null, null, null, null),
        };
        await h.RunScopeAsync(async sp =>
        {
            var service = ActivatorUtilities.CreateInstance<CourtService>(sp);
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            var before = await service.GetByIdAsync(h.Court1);
            foreach (var request in invalid)
            {
                await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateAsync(h.Court1, request, h.Manager));
                await db.SaveChangesAsync();
                Assert.Equal(before, await service.GetByIdAsync(h.Court1));
                await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(new(h.BranchId, "INVALID", request.Name ?? "Court",
                    request.PriceLowPeak ?? 100000, request.PricePeak ?? 150000,
                    request.WeekendSurcharge ?? 0.2m, request.Capacity ?? 2), h.Manager));
            }
            Assert.False(await db.Courts.AnyAsync(c => c.Code == "INVALID"));
            await service.UpdateAsync(h.Court1, new(null, 0, 150000, 0, 1, null, null, null), h.Manager);
            var valid = await service.GetByIdAsync(h.Court1);
            Assert.Equal(0, valid.PriceLowPeak);
            Assert.Equal(1, valid.Capacity);
        });
    }
}
