using Contract.Repositories.AuthEntity;
using System.ComponentModel.DataAnnotations.Schema;


namespace ModelViews.UserInfoModelView
{
    public class UserInfoResponseModelView
    {
        public Guid UserId { get; set; }

        public string GivenName { get; set; } = string.Empty;
        public string? FamilyName { get; set; }
        public string? Picture { get; set; }
        public string? Email { get; set; }
        //public string? Locale { get; set; }
        public string? Address { get; set; }
        public DateTime? BirthDate { get; set; }
        public GenderType Gender { get; set; } = GenderType.RatherNotSay;
        [NotMapped]
        public string FullName => string.IsNullOrWhiteSpace(FamilyName) ? GivenName : $"{GivenName} {FamilyName}";

    }
}
