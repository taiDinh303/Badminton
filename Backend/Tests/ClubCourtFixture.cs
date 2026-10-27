using Badminton.Core.Enums;
using Contract.Repositories.Entity;
using Contract.Services.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Context;
namespace Badminton.Tests;

public static class ClubCourtFixture
{
    public static async Task<int> CreateBookingAsync(TestHost h, DateTime start, DateTime end)
        => await h.RunScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            await db.Courts.Where(x => x.Id == h.Court1).ExecuteUpdateAsync(x => x.SetProperty(c => c.Capacity, 20));
            var startTime = TimeOnly.FromDateTime(start.AddHours(7));
            var endTime = TimeOnly.FromDateTime(end.AddHours(7));
            var slot = await db.TimeSlots.SingleOrDefaultAsync(x => x.StartTime == startTime && x.EndTime == endTime);
            if (slot is null)
            {
                slot = new TimeSlot { Label = "Class " + Guid.NewGuid().ToString("N")[..6], StartTime = startTime, EndTime = endTime, IsActive = true };
                db.TimeSlots.Add(slot);
                await db.SaveChangesAsync();
            }
            var booking = new Booking { BookingCode = "CLUB-" + Guid.NewGuid().ToString("N")[..12], UserId = h.Manager, CourtId = h.Court1, BranchId = h.BranchId, SlotId = slot.Id, Date = DateOnly.FromDateTime(start.AddHours(7)), Status = BookingStatus.Confirmed, TotalAmount = 100000, PayableAmount = 100000 };
            db.Bookings.Add(booking);
            await db.SaveChangesAsync();
            return booking.Id;
        });
    public static async Task<int> AttachAsync(TestHost h, int id)
    {
        var slot = await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Set<CoachSlot>().SingleAsync(x => x.Id == id));
        var booking = await CreateBookingAsync(h, slot.StartsAtUtc, slot.EndsAtUtc);
        await h.RunScopeAsync(sp => sp.GetRequiredService<ICoachingService>().AttachCourtAsync(id, booking));
        return booking;
    }
}
