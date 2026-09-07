using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Core.Base;

namespace Contract.Repositories.AuthEntity
{
    public class UserInfo : BaseEntity
    {
        [ForeignKey(nameof(Id))]
        public virtual ApplicationUser? User { get; set; }

        // Google: given_name
        [MaxLength(25)]
        [MinLength(1, ErrorMessage = "Given name must have at least 1 character.")]
        public string GivenName { get; set; } = string.Empty;


        public string? FamilyName { get; set; }

        // Google: picture
        public string? Picture { get; set; }

        // Google: email (optional, nếu cần lưu thêm)
        [EmailAddress]
        public string? Email { get; set; }

        // Google: locale
        [MaxLength(10)]
        public string? Locale { get; set; }

        // Custom fields
        public string? Address { get; set; }
        public DateTime? BirthDate { get; set; }

        public GenderType Gender { get; set; } = GenderType.RatherNotSay;
    }

    public enum GenderType
    {
        RatherNotSay = 0,
        Male = 1,
        Female = 2
    }
}
