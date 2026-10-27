using BookingEntity = Contract.Repositories.Entity.Booking;

namespace Contract.Services.Booking;

public interface IBookingCancellationAuthorization
{
    Task EnsureAuthorizedAsync(BookingEntity booking, int actorId);
}
