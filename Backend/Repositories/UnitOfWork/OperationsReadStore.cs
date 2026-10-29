using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;
namespace Repositories.UnitOfWork;

public sealed class OperationsReadStore(BadmintonBookingDbContext db) : IOperationsReadStore
{
    public async Task<IReadOnlyList<BookingCase>> CasesAsync(int? userId, int? branchId, int? bookingId, Page page)
        => await db.Set<BookingCase>().AsNoTracking()
            .Where(c => (userId == null || c.UserId == userId) && (bookingId == null || c.BookingId == bookingId)
                && (branchId == null || db.Bookings.Any(b => b.Id == c.BookingId && b.BranchId == branchId)))
            .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).Skip(page.Skip).Take(page.Size).ToListAsync();

    public async Task<IReadOnlyList<Notification>> DeliveriesAsync(int? branchId, Page page)
        => await db.Notifications.AsNoTracking()
            .Where(n => branchId == null || db.Bookings.Any(b => b.Id == n.BookingId && b.BranchId == branchId))
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Skip(page.Skip).Take(page.Size).ToListAsync();

    public async Task<IReadOnlyList<ReviewReport>> ReviewReportsAsync(int? branchId, Page page)
        => await db.Set<ReviewReport>().AsNoTracking()
            .Where(r => branchId == null || db.Reviews.Any(review => review.Id == r.ReviewId && db.Courts.Any(c => c.Id == review.CourtId && c.BranchId == branchId)))
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Skip(page.Skip).Take(page.Size).ToListAsync();

    public Task<int> ReturningCustomersAsync(int[] userIds, DateOnly before, int? branchId)
        => db.Bookings.AsNoTracking().Where(b => b.DeletedAt == null && userIds.Contains(b.UserId) && b.Date < before && (branchId == null || b.BranchId == branchId))
            .Select(b => b.UserId).Distinct().CountAsync();
}
