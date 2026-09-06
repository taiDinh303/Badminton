using Contract.Repositories.IOUW;
using Repositories.Context;
using Repositories.Repository;

namespace Repositories.UOW
{
    public class UOW : IUOW
    {
        private readonly BadmintonBookingDbContext _context;

        public IUserRepository Users { get; }

        public IBookingRepository Bookings { get; }

        public ICourtRepository Courts { get; }

        public IBookingDetailRepository BookingDetails { get; }

        public ITimeSlotRepository TimeSlots { get; }

        public IPaymentRepository Payments { get; }

        public IBankAccountRepository BankAccounts { get; }

        public IVerificationCodeRepository VerificationCodes { get; }

        public UOW(
            BadmintonBookingDbContext context,
            IUserRepository users,
            IBookingRepository bookings,
            ICourtRepository courts,
            IBookingDetailRepository bookingDetails,
            ITimeSlotRepository timeSlots,
            IPaymentRepository paymentRepository,
            IBankAccountRepository bankAccounts,
            IVerificationCodeRepository verificationCodes)
        {
            _context = context;
            Users = users;
            Bookings = bookings;
            Courts = courts;
            BookingDetails = bookingDetails;
            TimeSlots = timeSlots;
            Payments = paymentRepository;
            BankAccounts = bankAccounts;
            VerificationCodes = verificationCodes;
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}