using System.ComponentModel.DataAnnotations;

namespace ModelViews.UserInfoModelView
{
    public class UpdateUserModelView
    {
        public Guid Id { get; set; }
        [EmailAddress(ErrorMessage = "Email is invalid")]
        public string? Email { get; set; }
        [Phone(ErrorMessage = "Phone is invalid")]
        public string? PhoneNumber { get; set; }
    }
}
