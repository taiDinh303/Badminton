using Badminton.Core.Base;
using Badminton.Core.Enums;

namespace Contract.Repositories.Entity
{
    public class User : IntBaseEntity
    {
        public string FullName { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string? Email { get; set; }
        /// <summary>Dev fallback until Supabase Auth OTP is wired.</summary>
        public string PasswordHash { get; set; } = null!;
        public string? AvatarUrl { get; set; }

        public Role Role { get; set; } = Role.Customer;
        public int? BranchId { get; set; }
        public Branch? Branch { get; set; }
        public UserStatus Status { get; set; } = UserStatus.Active;
        public bool IsVerified { get; set; }

        public int NoShowCount { get; set; }
        public DateOnly NoShowPeriod { get; set; } = new(2026, 1, 1);
        public bool NoShowLimited { get; set; }
        public DateOnly? NoShowLimitedPeriod { get; set; }
    }
}
