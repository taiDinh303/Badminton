using Contract.Repositories.Entity;
using Contract.Repositories.IOUW;
using Contract.Services.Interface;
using ModelViews.Payment;

namespace Services.Service
{
    public class PaymentService : IPaymentService
    {
        private readonly IUOW _uow;

        public PaymentService(IUOW uow)
        {
            _uow = uow;
        }

        public async Task<PaymentResponse?> CreateAsync(
            int bookingId,
            int userId,
            string role,
            string paymentMethod)
        {
            if (paymentMethod != "Mock" &&
                paymentMethod != "Cash" &&
                paymentMethod != "Banking")
            {
                throw new Exception("Invalid payment method.");
            }

            var booking = await _uow.Bookings.GetByIdAsync(bookingId);

            if (booking == null)
                return null;

            if (role != "Admin" && booking.UserId != userId)
                return null;

            if (booking.Status != "Confirmed")
                throw new Exception(
                    "Only confirmed booking can be paid.");

            var existingPayment =
                await _uow.Payments.GetByBookingIdAsync(bookingId);

            if (existingPayment != null)
                throw new Exception(
                    "Payment already exists.");

            var payment = new Payment
            {
                BookingId = bookingId,
                Amount = booking.TotalAmount,
                PaymentMethod = paymentMethod,
                Status = "Pending"
            };

            await _uow.Payments.CreateAsync(payment);

            return MapToResponse(payment);
        }

        public async Task<PaymentResponse?> GetByBookingIdAsync(
            int bookingId,
            int userId,
            string role)
        {
            var booking = await _uow.Bookings.GetByIdAsync(bookingId);

            if (booking == null)
                return null;

            if (role != "Admin" && booking.UserId != userId)
                return null;

            var payment =
                await _uow.Payments.GetByBookingIdAsync(bookingId);

            if (payment == null)
                return null;

            return MapToResponse(payment);
        }

        public async Task<bool> MarkAsPaidAsync(
            int bookingId,
            int userId,
            string role,
            string transactionCode)
        {
            var booking = await _uow.Bookings.GetByIdAsync(bookingId);

            if (booking == null)
                return false;

            if (role != "Admin" && booking.UserId != userId)
                return false;

            if (booking.Status != "Confirmed")
                return false;

            var payment =
                await _uow.Payments.GetByBookingIdAsync(bookingId);

            if (payment == null)
                return false;

            var result = await _uow.Payments.MarkAsPaidAsync(
                bookingId,
                transactionCode);

            if (!result)
                return false;

            booking.Status = "Paid";
            booking.UpdatedAt = DateTime.UtcNow;

            await _uow.SaveChangesAsync();

            return true;
        }

        private static PaymentResponse MapToResponse(Payment payment)
        {
            return new PaymentResponse
            {
                PaymentId = payment.PaymentId,
                BookingId = payment.BookingId,
                Amount = payment.Amount,
                PaymentMethod = payment.PaymentMethod,
                Status = payment.Status,
                TransactionCode = payment.TransactionCode,
                CreatedAt = payment.CreatedAt,
                PaidAt = payment.PaidAt
            };
        }
    }
}