using Contract.Repositories.Entity;
using Contract.Repositories.IOUW;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;

namespace Repositories.UOW
{
    public class BookingRepository : IBookingRepository
    {
        private readonly BadmintonBookingDbContext _context;

        public BookingRepository(BadmintonBookingDbContext context)
        {
            _context = context;
        }

        public async Task<Booking?> GetByIdAsync(int bookingId)
        {
            return await _context.Bookings
                .Include(x => x.BookingDetails)
                    .ThenInclude(x => x.Court)
                .Include(x => x.BookingDetails)
                    .ThenInclude(x => x.TimeSlot)
                .Include(x => x.Payment)
                .FirstOrDefaultAsync(x => x.BookingId == bookingId);
        }

        public async Task<List<Booking>> GetByUserIdAsync(int userId)
        {
            return await _context.Bookings
                .Include(x => x.BookingDetails)
                    .ThenInclude(x => x.Court)
                .Include(x => x.BookingDetails)
                    .ThenInclude(x => x.TimeSlot)
                .Include(x => x.Payment)
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.BookingDate)
                .ToListAsync();
        }

        public async Task<Booking> CreateAsync(Booking booking)
        {
            _context.Bookings.Add(booking);

            await _context.SaveChangesAsync();

            return booking;
        }

        public async Task<bool> HasBookedAsync(
            int courtId,
            int timeSlotId,
            DateTime bookingDate)
        {
            return await _context.BookingDetails.AnyAsync(x =>
                x.CourtId == courtId &&
                x.TimeSlotId == timeSlotId &&
                x.BookingDate == bookingDate &&
                x.Status == "Reserved");
        }

        public async Task<List<int>> GetBookedTimeSlotIdsAsync(
            int courtId,
            DateTime bookingDate)
        {
            return await _context.BookingDetails
                .Where(x =>
                    x.CourtId == courtId &&
                    x.BookingDate.Date == bookingDate.Date &&
                    x.Status == "Reserved")
                .Select(x => x.TimeSlotId)
                .ToListAsync();
        }

        public async Task<bool> ConfirmAsync(int bookingId)
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(x => x.BookingId == bookingId);

            if (booking == null)
                return false;

            if (booking.Status != "Pending")
                return false;

            booking.Status = "Confirmed";
            booking.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> CancelAsync(int bookingId)
        {
            var booking = await _context.Bookings
                .Include(x => x.BookingDetails)
                .FirstOrDefaultAsync(x => x.BookingId == bookingId);

            if (booking == null)
                return false;

            if (booking.Status == "Cancelled")
                return false;

            booking.Status = "Cancelled";
            booking.UpdatedAt = DateTime.UtcNow;

            foreach (var detail in booking.BookingDetails)
            {
                if (detail.Status == "Reserved")
                {
                    detail.Status = "Cancelled";
                }
            }

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> CompleteAsync(int bookingId)
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(x => x.BookingId == bookingId);

            if (booking == null)
                return false;

            if (booking.Status != "Paid")
                return false;

            booking.Status = "Completed";
            booking.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<List<Booking>> GetAllAsync()
        {
            return await _context.Bookings
                .Include(x => x.BookingDetails)
                    .ThenInclude(x => x.Court)
                .Include(x => x.BookingDetails)
                    .ThenInclude(x => x.TimeSlot)
                .Include(x => x.Payment)
                .OrderByDescending(x => x.BookingDate)
                .ThenByDescending(x => x.CreatedAt)
                .ToListAsync();
        }
    }
}