using Contract.Repositories.Entity;
using System.ComponentModel.DataAnnotations;


namespace ModelViews.UserInfoModelView
{
    public class CreateUserInfoModelView
    {
        [Required]
        public Guid UserId { get; set; }

        // Google: given_name
        [MaxLength(25)]
        [MinLength(1, ErrorMessage = "Given name must have at least 1 character.")]
        public string GivenName { get; set; } = string.Empty;

        // Google: family_name
        [MaxLength(25)]
        public string? FamilyName { get; set; }

        // Google: picture
        public string? Picture { get; set; }

        // Google: email
        [EmailAddress]
        public string? Email { get; set; }

        public DateTime? BirthDate { get; set; }

        public GenderType Gender { get; set; } = GenderType.RatherNotSay;
    }
}
