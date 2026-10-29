using Badminton.Core.Base;
using Badminton.Core.Enums;

namespace Contract.Repositories.Entity
{
    public class Notification : IntBaseEntity
    {
        public int UserId
        {
            get; set;
        }
        public User? User
        {
            get; set;
        }
        public NotifyEvent Event
        {
            get; set;
        }
        public int? BookingId
        {
            get; set;
        }
        public Booking? Booking
        {
            get; set;
        }
        public string LedgerKey { get; set; } = null!;
        public string Channel { get; set; } = "email";
        public NotifyStatus Status { get; set; } = NotifyStatus.Pending;
        public string? ExternalRef
        {
            get; set;
        }
        public string? Body
        {
            get; set;
        }
        public DateTime? SentAt
        {
            get; set;
        }
        public int Attempts
        {
            get; set;
        }
        public string? LastError
        {
            get; set;
        }
    }
}
