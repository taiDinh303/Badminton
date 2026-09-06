namespace Contract.Repositories.IOUW
{
    public interface IUOW
    {
        IUserRepository Users { get; }
        ICourtRepository Courts { get; }
        IVerificationCodeRepository VerificationCodes { get; }
        IBookingRepository Bookings { get; }
        IBookingDetailRepository BookingDetails { get; }
        IPaymentRepository Payments { get; }
        ITimeSlotRepository TimeSlots { get; }

        IBankAccountRepository BankAccounts { get; }

        Task<int> SaveChangesAsync();
    }
}