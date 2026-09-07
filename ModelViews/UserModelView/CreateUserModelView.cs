using ModelViews.UserInfoModelView;

namespace ModelViews.UserModelView
{
    public class CreateUserModelView
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "User";
        public CreateUserInfoModelView? CreateUserInfoModelView { get; set; }
    }
}
