using Contract.Repositories.Entity;

namespace Contract.Repositories.IOUW
{
    public interface IBookingRepository
    {
        Task<bool> HasBookedAsync(
            int courtId,
            int timeSlotId,
            DateTime bookingDate);

        Task<Booking> CreateAsync(Booking booking);

        Task<Booking?> GetByIdAsync(int bookingId);

        Task<List<Booking>> GetAllAsync();

        Task<List<Booking>> GetByUserIdAsync(int userId);

        Task<List<int>> GetBookedTimeSlotIdsAsync(
            int courtId,
            DateTime bookingDate);

        Task<bool> ConfirmAsync(int bookingId);

        Task<bool> CancelAsync(int bookingId);

        Task<bool> CompleteAsync(int bookingId);
    }
}