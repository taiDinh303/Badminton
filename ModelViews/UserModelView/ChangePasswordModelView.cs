namespace ModelViews.UserModelView
{
    public class ChangePasswordModelView
    {
        public Guid UserId { get; set; }
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
