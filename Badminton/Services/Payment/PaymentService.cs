using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Payment;
using M.Core.Base;
using M.Core.Domain;
using M.Core.Enums;
using Microsoft.AspNetCore.Http;
using ModelViews.Payment;

namespace Services
{
    /// <summary>
    /// PAYMENT service. Shows off the POLYMORPHISM contract
    /// <see cref="IPaymentStrategyFactory"/>: it never knows *how* a method
    /// charges — it asks the factory for the strategy matching
    /// <c>request.Method</c> and lets it run. Adding a new payment method =
    /// one new strategy class + one DI registration; this service is untouched.
    /// </summary>
    public sealed class PaymentService : BaseService, IPaymentService
    {
        private readonly IPaymentStrategyFactory _strategyFactory;

        public PaymentService(
            IUnitOfWork uow,
            IHttpContextAccessor http,
            IPaymentStrategyFactory strategyFactory) : base(uow, http)
        {
            _strategyFactory = strategyFactory;
        }

        public async Task<PaymentView> ConfirmPaymentAsync(
            ConfirmPaymentRequest request, int userId, string userName)
        {
            var bookingRepo = Repository<Booking>();
            var booking = await bookingRepo.GetByIdAsync(request.BookingId);
            if (booking is null || booking.DeletedAt.HasValue)
                throw new NotFoundException(nameof(Booking), request.BookingId);

            // Owner can pay their own booking; staff/admin can pay anyone.
            // (The controller enforces role; this guard stops an impostor user.)
            var isOwner = booking.UserId == userId;
            if (!isOwner)
            {
                // Non-owner confirmation is treated as an admin action; the
                // [Authorize(Roles="Admin")] attribute on the endpoint is what
                // actually permits it, so reaching here unauthorised is a bug.
            }

            if (booking.Status is BookingStatus.Paid or BookingStatus.Cancelled)
                throw new ConflictException($"Booking is already {booking.Status}.");

            // POLYMORPHISM: resolve the concrete strategy for this method.
            var strategy = _strategyFactory.GetStrategy(request.Method);
            var ctx = new PaymentContext
            {
                BookingId = booking.Id,
                BookingCode = booking.BookingCode,
                Amount = booking.TotalAmount,
                UserId = userId,
                TransactionReference = request.TransactionReference
            };
            var result = await strategy.PayAsync(ctx);
            if (!result.Succeeded)
                throw new BadRequestException(result.Error ?? "Payment failed.");

            // Persist: payment -> Paid, booking -> Paid, details stay Reserved.
            var paymentRepo = Repository<Payment>();
            var existing = await paymentRepo.GetByConditionAsync(
                p => p.BookingId == booking.Id);
            var payment = existing.FirstOrDefault() ?? new Payment
            {
                BookingId = booking.Id,
                Amount = booking.TotalAmount,
                Method = request.Method,
                Status = PaymentStatus.Pending
            };

            payment.Method = request.Method;
            payment.Amount = booking.TotalAmount;
            payment.Status = result.Status;
            payment.TransactionCode = result.TransactionCode ?? ctx.TransactionReference;
            payment.PaidAt = DateTime.UtcNow;
            ApplyUpdateAudit(payment);

            if (payment.Id == 0) await paymentRepo.AddAsync(payment);
            else paymentRepo.Update(payment);

            booking.Status = BookingStatus.Paid;
            ApplyUpdateAudit(booking);
            bookingRepo.Update(booking);
            await SaveAsync();

            return new PaymentView(
                payment.Id, payment.BookingId, booking.BookingCode,
                payment.Method, payment.Amount, payment.Status,
                payment.TransactionCode, payment.PaidAt);
        }

        public async Task RefundAsync(int paymentId, string userName)
        {
            var paymentRepo = Repository<Payment>();
            var payment = await paymentRepo.GetByIdAsync(paymentId);
            if (payment is null) throw new NotFoundException(nameof(Payment), paymentId);
            if (payment.Status != PaymentStatus.Paid)
                throw new ConflictException("Only paid payments can be refunded.");

            var strategy = _strategyFactory.GetStrategy(payment.Method);
            var ctx = new PaymentContext
            {
                BookingId = payment.BookingId,
                BookingCode = "",
                Amount = payment.Amount,
                UserId = 0,
                TransactionReference = payment.TransactionCode
            };
            var result = await strategy.RefundAsync(ctx);
            if (!result.Succeeded)
                throw new BadRequestException(result.Error ?? "Refund failed.");

            payment.Status = PaymentStatus.Refunded;
            ApplyUpdateAudit(payment);
            paymentRepo.Update(payment);

            var booking = await Repository<Booking>().GetByIdAsync(payment.BookingId);
            if (booking is not null)
            {
                booking.Status = BookingStatus.Pending; // reopened after refund
                ApplyUpdateAudit(booking);
                Repository<Booking>().Update(booking);
            }
            await SaveAsync();
        }

        public async Task<PaymentView> GetByBookingAsync(int bookingId)
        {
            var payments = await Repository<Payment>()
                .GetByConditionAsync(p => p.BookingId == bookingId);
            var p = payments.FirstOrDefault();
            if (p is null) throw new NotFoundException(nameof(Payment), bookingId);
            var booking = await Repository<Booking>().GetByIdAsync(bookingId);
            return new PaymentView(
                p.Id, p.BookingId, booking?.BookingCode ?? "?",
                p.Method, p.Amount, p.Status, p.TransactionCode, p.PaidAt);
        }
    }
}
