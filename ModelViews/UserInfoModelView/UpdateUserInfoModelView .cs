using System.ComponentModel.DataAnnotations;
using Contract.Repositories.AuthEntity;

namespace ModelViews.UserInfoModelView
{
    public class UpdateUserInfoModelView
    {
        public Guid UserId { get; set; }

        public string? GivenName { get; set; }

        public string? FamilyName { get; set; }

        public string? Picture { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        public string? Locale { get; set; }
        public string? Address { get; set; }
        public DateTime? BirthDate { get; set; }
        public GenderType? Gender { get; set; }
    }

    public class UpdateNameModelView
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        public string GivenName { get; set; } = string.Empty;

        [MaxLength(25)]
        public string? FamilyName { get; set; }
    }

    // Update gender
    public class UpdateGenderModelView
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        public GenderType Gender { get; set; }
    }

    // Update birthday
    public class UpdateBirthdayModelView
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        public DateTime BirthDate { get; set; }
    }

}
