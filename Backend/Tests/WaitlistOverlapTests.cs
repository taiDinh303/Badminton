using Badminton.Core.Base;
using Badminton.Core.Enums;
using Contract.Repositories.Entity;
using Contract.Services.Booking;
using Contract.Services.Waitlist;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Context;
using Xunit;

namespace Badminton.Tests;

public sealed class WaitlistOverlapTests
{
    [Theory]
    [InlineData(12, 15, 12, 45, true)]
    [InlineData(11, 30, 13, 30, true)]
    [InlineData(12, 30, 13, 30, true)]
    [InlineData(11, 0, 12, 0, false)]
    [InlineData(13, 0, 14, 0, false)]
    public async Task Availability_creation_and_waitlist_agree_on_legacy_interval_boundaries(int sh, int sm, int eh, int em, bool overlaps)
    {
        await using var h = new TestHost();
        var legacy = await AddSlot(h, new(sh, sm), new(eh, em));
        await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon);
        h.ActAs(h.Bob, Role.Customer);
        var shown = await Availability(h, legacy);
        Assert.Equal(overlaps, shown.IsLocked);
        if (overlaps)
        {
            Assert.Equal("Đã đặt", shown.LockReason);
            await Assert.ThrowsAsync<ConflictException>(() => h.BookAsync(h.Bob, h.Court1, legacy, h.FutureDate.ToString("yyyy-MM-dd")));
            var entry = await h.RunScopeAsync(sp => sp.GetRequiredService<IWaitlistService>().JoinAsync(h.Bob, h.Court1, legacy, h.FutureDate, 1, null));
            Assert.Equal(WaitlistStatus.Waiting, entry.Status);
        }
        else
        {
            await Assert.ThrowsAsync<BadRequestException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IWaitlistService>().JoinAsync(h.Bob, h.Court1, legacy, h.FutureDate, 1, null)));
            Assert.Equal("Pending", (await h.BookAsync(h.Bob, h.Court1, legacy, h.FutureDate.ToString("yyyy-MM-dd"))).Status);
        }
    }

    [Theory]
    [InlineData(BookingStatus.Pending, true)]
    [InlineData(BookingStatus.Confirmed, true)]
    [InlineData(BookingStatus.Cancelled, false)]
    [InlineData(BookingStatus.Completed, false)]
    [InlineData(BookingStatus.NoShow, false)]
    [InlineData(BookingStatus.Refunded, false)]
    public async Task Only_active_bookings_block_overlapping_waitlist_slots(BookingStatus status, bool blocked)
    {
        await using var h = new TestHost();
        var legacy = await AddSlot(h, new(12, 15), new(12, 45));
        var booking = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"));
        await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings.Where(b => b.Id == booking.Id).ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, status)));
        h.ActAs(h.Bob, Role.Customer);
        Assert.Equal(blocked, (await Availability(h, legacy)).IsLocked);
        if (blocked)
            Assert.Equal(WaitlistStatus.Waiting, (await h.RunScopeAsync(sp => sp.GetRequiredService<IWaitlistService>().JoinAsync(h.Bob, h.Court1, legacy, h.FutureDate, 1, null))).Status);
        else
            await Assert.ThrowsAsync<BadRequestException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IWaitlistService>().JoinAsync(h.Bob, h.Court1, legacy, h.FutureDate, 1, null)));
    }

    [Fact]
    public async Task A_waiter_keeps_their_place_until_all_overlapping_bookings_are_released_then_is_promoted_once()
    {
        await using var h = new TestHost();
        var legacy = await AddSlot(h, new(12, 15), new(12, 45));
        var booking = await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon);
        h.ActAs(h.Bob, Role.Customer);
        var entry = await h.RunScopeAsync(sp => sp.GetRequiredService<IWaitlistService>().JoinAsync(h.Bob, h.Court1, legacy, h.FutureDate, 1, null));
        Assert.Null(await h.RunScopeAsync(sp => sp.GetRequiredService<IWaitlistService>().PromoteAsync(h.Court1, legacy, h.FutureDate)));
        Assert.Equal(WaitlistStatus.Waiting, (await h.RunScopeAsync(sp => sp.GetRequiredService<IWaitlistService>().GetMineAsync(h.Bob))).Single().Status);
        h.ActAs(h.Alice, Role.Customer);
        await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().CancelAsync(booking.Id, h.Alice));
        var promoted = await h.RunScopeAsync(sp => sp.GetRequiredService<IWaitlistService>().PromoteAsync(h.Court1, legacy, h.FutureDate));
        Assert.NotNull(promoted);
        Assert.Equal(legacy, promoted.SlotId);
        Assert.Equal(h.Bob, promoted.UserId);
        Assert.Equal(BookingStatus.Pending, promoted.Status);
        Assert.Null(await h.RunScopeAsync(sp => sp.GetRequiredService<IWaitlistService>().PromoteAsync(h.Court1, legacy, h.FutureDate)));
        var final = (await h.RunScopeAsync(sp => sp.GetRequiredService<IWaitlistService>().GetMineAsync(h.Bob))).Single();
        Assert.Equal(entry.Id, final.Id);
        Assert.Equal(WaitlistStatus.Promoted, final.Status);
        Assert.Equal(promoted.Id, final.PromotedBookingId);
    }

    private static Task<int> AddSlot(TestHost h, TimeOnly start, TimeOnly end) => h.RunScopeAsync(async sp => {
        var db = sp.GetRequiredService<BadmintonBookingDbContext>();
        var slot = new TimeSlot { Label = "Legacy overlap", StartTime = start, EndTime = end, IsActive = true };
        db.TimeSlots.Add(slot); await db.SaveChangesAsync(); return slot.Id;
    });

    private static async Task<ModelViews.Booking.SlotView> Availability(TestHost h, int slotId) =>
        (await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().GetAvailabilityAsync(h.BranchId, h.FutureDate)))
            .Single(c => c.CourtId == h.Court1).Slots.Single(s => s.SlotId == slotId);
}
