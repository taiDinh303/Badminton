using Contract.Repositories.Entity;
using Contract.Repositories.IOUW;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;

namespace Repositories.UOW
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly BadmintonBookingDbContext _context;

        public PaymentRepository(BadmintonBookingDbContext context)
        {
            _context = context;
        }

        public async Task<Payment?> GetByBookingIdAsync(int bookingId)
        {
            return await _context.Payments
                .FirstOrDefaultAsync(x => x.BookingId == bookingId);
        }

        public async Task<Payment> CreateAsync(Payment payment)
        {
            await _context.Payments.AddAsync(payment);
            await _context.SaveChangesAsync();

            return payment;
        }

        public async Task<bool> MarkAsPaidAsync(
            int bookingId,
            string transactionCode)
        {
            var payment = await _context.Payments
                .FirstOrDefaultAsync(x => x.BookingId == bookingId);

            if (payment == null)
                return false;

            if (payment.Status == "Paid")
                return false;

            payment.Status = "Paid";
            payment.TransactionCode = transactionCode;
            payment.PaidAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return true;
        }
    }
}