using Contract.Repositories.Entity;
using Contract.Repositories.IOUW;
using Contract.Services.Interface;
using ModelViews.Booking;
using ModelViews.Payment;

namespace Services.Service
{
    public class BookingService : IBookingService
    {
        private readonly IUOW _uow;

        public BookingService(IUOW uow)
        {
            _uow = uow;
        }

        public async Task<BookingResponse> CreateAsync(
            CreateBookingRequest request,
            int userId)
        {
            if (request.BookingDate < DateOnly.FromDateTime(DateTime.Today))
                throw new Exception("Booking date cannot be in the past.");

            if (request.Details == null || request.Details.Count == 0)
                throw new Exception("Booking must contain at least one court.");

            var duplicate = request.Details
                .GroupBy(x => new { x.CourtId, x.TimeSlotId })
                .Any(x => x.Count() > 1);

            if (duplicate)
                throw new Exception("Duplicate court and time slot.");

            var booking = new Booking
            {
                BookingCode = $"BK{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                UserId = userId,
                BookingDate = request.BookingDate.ToDateTime(TimeOnly.MinValue),
                Status = "Pending",
                Note = request.Note
            };

            foreach (var detail in request.Details)
            {
                var court = await _uow.Courts.GetByIdAsync(detail.CourtId);

                if (court == null)
                    throw new Exception("Court not found.");

                var timeSlot = await _uow.TimeSlots.GetByIdAsync(detail.TimeSlotId);

                if (timeSlot == null)
                    throw new Exception("Time slot not found.");

                if (request.BookingDate == DateOnly.FromDateTime(DateTime.Today))
                {
                    if (timeSlot.StartTime <= DateTime.Now.TimeOfDay)
                        throw new Exception("This time slot has already started.");
                }

                var hasBooked = await _uow.Bookings.HasBookedAsync(
                    detail.CourtId,
                    detail.TimeSlotId,
                    request.BookingDate.ToDateTime(TimeOnly.MinValue));

                if (hasBooked)
                    throw new Exception("Court is already booked.");

                booking.BookingDetails.Add(new BookingDetail
                {
                    CourtId = detail.CourtId,
                    TimeSlotId = detail.TimeSlotId,
                    BookingDate = request.BookingDate.ToDateTime(TimeOnly.MinValue),
                    Price = court.PricePerHour,
                    Status = "Reserved"
                });

                booking.TotalAmount += court.PricePerHour;
            }

            await _uow.Bookings.CreateAsync(booking);

            return MapToResponse(booking);
        }

        public async Task<BookingResponse?> GetByIdAsync(
            int bookingId,
            int userId,
            string role)
        {
            var booking = await _uow.Bookings.GetByIdAsync(bookingId);

            if (booking == null)
                return null;

            if (role != "Admin" &&
                role != "Staff" &&
                booking.UserId != userId)
            {
                return null;
            }

            return MapToResponse(booking);
        }

        public async Task<List<BookingResponse>> GetMyBookingsAsync(int userId)
        {
            var bookings = await _uow.Bookings.GetByUserIdAsync(userId);

            return bookings
                .Select(MapToResponse)
                .ToList();
        }

        public async Task<List<AvailableCourtResponse>> GetAvailableAsync(
            DateTime bookingDate)
        {
            if (bookingDate.Date < DateTime.Today)
                throw new Exception("Booking date cannot be in the past.");

            var courts = await _uow.Courts.GetAllAsync();
            var timeSlots = await _uow.TimeSlots.GetAllAsync();

            var result = new List<AvailableCourtResponse>();

            var isToday = bookingDate.Date == DateTime.Today;
            var currentTime = DateTime.Now.TimeOfDay;

            foreach (var court in courts)
            {
                var bookedTimeSlotIds =
                    await _uow.Bookings.GetBookedTimeSlotIdsAsync(
                        court.CourtId,
                        bookingDate);

                result.Add(new AvailableCourtResponse
                {
                    CourtId = court.CourtId,
                    CourtCode = court.CourtCode,
                    CourtName = court.CourtName,
                    PricePerHour = court.PricePerHour,

                    TimeSlots = timeSlots.Select(slot =>
                        new AvailableTimeSlotResponse
                        {
                            TimeSlotId = slot.TimeSlotId,
                            StartTime = slot.StartTime,
                            EndTime = slot.EndTime,
                            IsAvailable =
                                !bookedTimeSlotIds.Contains(slot.TimeSlotId)
                                && (!isToday ||
                                    slot.StartTime > currentTime)
                        }).ToList()
                });
            }

            return result;
        }

        public async Task<bool> ConfirmAsync(
            int bookingId,
            int userId,
            string role)
        {
            var booking = await _uow.Bookings.GetByIdAsync(bookingId);

            if (booking == null)
                return false;

            if (role != "Admin" &&
                role != "Staff" &&
                booking.UserId != userId)
            {
                return false;
            }

            if (booking.Status != "Pending")
                return false;

            return await _uow.Bookings.ConfirmAsync(bookingId);
        }

        public async Task<bool> CancelAsync(
            int bookingId,
            int userId,
            string role)
        {
            var booking = await _uow.Bookings.GetByIdAsync(bookingId);

            if (booking == null)
                return false;

            if (role != "Admin" &&
                booking.UserId != userId)
            {
                return false;
            }

            if (booking.Status != "Pending" &&
                booking.Status != "Confirmed")
            {
                return false;
            }

            return await _uow.Bookings.CancelAsync(bookingId);
        }

        public async Task<bool> CompleteAsync(
            int bookingId,
            int userId,
            string role)
        {
            var booking = await _uow.Bookings.GetByIdAsync(bookingId);

            if (booking == null)
                return false;

            if (role != "Admin" &&
                role != "Staff")
            {
                return false;
            }

            if (booking.Status != "Paid")
                return false;

            return await _uow.Bookings.CompleteAsync(bookingId);
        }

        private static BookingResponse MapToResponse(Booking booking)
        {
            return new BookingResponse
            {
                BookingId = booking.BookingId,
                BookingCode = booking.BookingCode,
                UserId = booking.UserId,
                BookingDate = booking.BookingDate,
                TotalAmount = booking.TotalAmount,
                Status = booking.Status,
                Note = booking.Note,

                Details = booking.BookingDetails
                    .Select(x => new BookingDetailResponse
                    {
                        BookingDetailId = x.BookingDetailId,
                        CourtId = x.CourtId,
                        CourtName = x.Court.CourtName,
                        TimeSlotId = x.TimeSlotId,
                        StartTime = x.TimeSlot.StartTime,
                        EndTime = x.TimeSlot.EndTime,
                        BookingDate = x.BookingDate,
                        Price = x.Price,
                        Status = x.Status
                    })
                    .ToList(),

                Payment = booking.Payment == null
                    ? null
                    : new PaymentResponse
                    {
                        PaymentId = booking.Payment.PaymentId,
                        BookingId = booking.Payment.BookingId,
                        Amount = booking.Payment.Amount,
                        PaymentMethod = booking.Payment.PaymentMethod,
                        Status = booking.Payment.Status,
                        TransactionCode = booking.Payment.TransactionCode,
                        CreatedAt = booking.Payment.CreatedAt,
                        PaidAt = booking.Payment.PaidAt
                    }
            };
        }

        public async Task<List<BookingResponse>> GetByUserIdAsync(int userId)
        {
            var bookings = await _uow.Bookings.GetByUserIdAsync(userId);

            return bookings
                .Select(MapToResponse)
                .ToList();
        }

        public async Task<List<BookingResponse>> GetAllAsync()
        {
            var bookings = await _uow.Bookings.GetAllAsync();

            return bookings
                .Select(MapToResponse)
                .ToList();
        }
    }
}