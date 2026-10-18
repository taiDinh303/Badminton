using Contract.Repositories.Entity;
using Badminton.Core.Base;
using Badminton.Core.Enums;

namespace Contract.Services.Booking
{
    public interface ICheckinService
    {
        Task<Contract.Repositories.Entity.Booking> CheckinByTokenAsync(string qrToken, int actorId);
        Task<Contract.Repositories.Entity.Booking> CheckinByCodeAsync(string bookingCode, int actorId);
        Task<IReadOnlyList<Contract.Repositories.Entity.Booking>> GetUnconfirmedForSlotAsync(
            int courtId, DateOnly date, string slotLabel);
    }
}
