using M.Core.Base;

namespace Contract.Repositories.Entity
{
    /// <summary>
    /// In-app notification for a user (booking reminders, payment updates).
    /// INHERITANCE: extends <see cref="IntBaseEntity"/>.
    /// </summary>
    public class Notification : IntBaseEntity
    {
        public int UserId { get; set; }
        public User? User { get; set; }

        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;

        /// <summary>Category: booking, payment, reminder, ...</summary>
        public string Type { get; set; } = "Info";
        public bool IsRead { get; set; }
    }
}
