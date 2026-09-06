using Contract.Repositories.Entity;
using Contract.Repositories.IOUW;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;

namespace Repositories.UOW
{
    public class BookingDetailRepository : IBookingDetailRepository
    {
        private readonly BadmintonBookingDbContext _context;

        public BookingDetailRepository(BadmintonBookingDbContext context)
        {
            _context = context;
        }

        public async Task<BookingDetail?> GetByIdAsync(int bookingDetailId)
        {
            return await _context.BookingDetails
                .Include(x => x.Booking)
                .Include(x => x.Court)
                .Include(x => x.TimeSlot)
                .FirstOrDefaultAsync(x =>
                    x.BookingDetailId == bookingDetailId);
        }

        public async Task<List<BookingDetail>> GetByBookingIdAsync(int bookingId)
        {
            return await _context.BookingDetails
                .Include(x => x.Court)
                .Include(x => x.TimeSlot)
                .Where(x => x.BookingId == bookingId)
                .ToListAsync();
        }

        public async Task<bool> CancelAsync(int bookingDetailId)
        {
            var detail = await _context.BookingDetails
                .Include(x => x.Booking)
                .FirstOrDefaultAsync(x => x.BookingDetailId == bookingDetailId);

            if (detail == null)
                return false;

            if (detail.Status == "Cancelled")
                return false;

            detail.Status = "Cancelled";

            var totalAmount = await _context.BookingDetails
                .Where(x =>
                    x.BookingId == detail.BookingId &&
                    x.Status == "Reserved")
                .SumAsync(x => x.Price);

            detail.Booking.TotalAmount = totalAmount;

            var hasReservedDetail = await _context.BookingDetails
                .AnyAsync(x =>
                    x.BookingId == detail.BookingId &&
                    x.Status == "Reserved");

            if (!hasReservedDetail)
            {
                detail.Booking.Status = "Cancelled";
            }

            await _context.SaveChangesAsync();

            return true;
        }
    }
}