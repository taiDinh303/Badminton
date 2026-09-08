using Contract.Repositories.IOUW;
using Repositories.Context;

namespace Repositories.UOW
{
    public class UOW : IUOW
    {
        private readonly DatabaseContext _context;

        public IUserRepository Users { get; }

        public IBookingRepository Bookings { get; }

        public ICourtRepository Courts { get; }

        public IBookingDetailRepository BookingDetails { get; }

        public ITimeSlotRepository TimeSlots { get; }

        public IPaymentRepository Payments { get; }

        public IBankAccountRepository BankAccounts { get; }

        public IBranchRepository Branches { get; }

        public ICourtTypeRepository CourtTypes { get; }

        public ICalendarTypeRepository CalendarTypes { get; }

        public IVerificationCodeRepository VerificationCodes { get; }

        public UOW(
            DatabaseContext context,
            IUserRepository users,
            IBookingRepository bookings,
            ICourtRepository courts,
            IBookingDetailRepository bookingDetails,
            ITimeSlotRepository timeSlots,
            IPaymentRepository paymentRepository,
            IBankAccountRepository bankAccounts,
            IBranchRepository branches,
            ICourtTypeRepository courtTypes,
            ICalendarTypeRepository calendarTypes,
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
            Branches = branches;
            CourtTypes = courtTypes;
            CalendarTypes = calendarTypes;
            VerificationCodes = verificationCodes;
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}