using Contract.Repositories.Entity;
using Contract.Repositories.IOUW;
using Contract.Services.Interface;
using ModelViews.Booking;

namespace Services.Service
{
    public class BookingDetailService : IBookingDetailService
    {
        private readonly IUOW _uow;

        public BookingDetailService(IUOW uow)
        {
            _uow = uow;
        }

        public async Task<BookingDetailResponse?> GetByIdAsync(
            int bookingDetailId,
            int userId,
            string role)
        {
            var detail = await _uow.BookingDetails
                .GetByIdAsync(bookingDetailId);

            if (detail == null)
                return null;

            if (!CanAccess(detail.Booking.UserId, userId, role))
                return null;

            return MapToResponse(detail);
        }

        public async Task<List<BookingDetailResponse>?> GetByBookingIdAsync(
            int bookingId,
            int userId,
            string role)
        {
            var booking = await _uow.Bookings
                .GetByIdAsync(bookingId);

            if (booking == null)
                return null;

            if (!CanAccess(booking.UserId, userId, role))
                return null;

            var details = await _uow.BookingDetails
                .GetByBookingIdAsync(bookingId);

            return details
                .Select(MapToResponse)
                .ToList();
        }

        public async Task<bool> CancelAsync(
            int bookingDetailId,
            int userId,
            string role)
        {
            var detail = await _uow.BookingDetails
                .GetByIdAsync(bookingDetailId);

            if (detail == null)
                return false;

            if (!CanAccess(detail.Booking.UserId, userId, role))
                return false;

            if (detail.Status != "Reserved")
                return false;

            return await _uow.BookingDetails
                .CancelAsync(bookingDetailId);
        }

        private static bool CanAccess(
            int bookingUserId,
            int userId,
            string role)
        {
            return role == "Admin" ||
                   role == "Staff" ||
                   bookingUserId == userId;
        }

        private static BookingDetailResponse MapToResponse(
            BookingDetail detail)
        {
            return new BookingDetailResponse
            {
                BookingDetailId = detail.BookingDetailId,
                CourtId = detail.CourtId,
                TimeSlotId = detail.TimeSlotId,
                BookingDate = detail.BookingDate,
                Price = detail.Price,
                Status = detail.Status
            };
        }
    }
}