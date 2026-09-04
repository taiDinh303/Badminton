using Core.Base;
using Contract.Repositories.BookingEntity;
using Contract.Repositories.Entity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Contract.Repositories.BookingEntity
{
    public class UserInfo : BaseEntity
    {
        [ForeignKey(nameof(Id))]
        public virtual User? User { get; set; }

        [MaxLength(25)]
        [MinLength(1, ErrorMessage = "Given name must have at least 1 character.")]
        public string GivenName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? FamilyName { get; set; }

        public string FullName => $"{GivenName} {FamilyName}".Trim();

        public string? Picture { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [Phone]
        public string? PhoneNumber { get; set; }

        [MaxLength(10)]
        public string? Locale { get; set; }

        public string? Address { get; set; }

        public DateTime? BirthDate { get; set; }

        public GenderType Gender { get; set; } = GenderType.RatherNotSay;

        public DateTime? LastLoginAt { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ICollection<Booking> Bookings { get; set; }
            = new List<Booking>();
    }

    public enum GenderType
    {
        RatherNotSay = 0,
        Male = 1,
        Female = 2
    }
}