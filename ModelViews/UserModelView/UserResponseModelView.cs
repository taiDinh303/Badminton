namespace ModelViews.UserInfoModelView
{
    public class UserResponseModelView
    {
        public Guid Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public bool EmailConfirmed { get; set; }
        public bool HasPassword { get; set; }
        //UserInfo
        public UserInfoResponseModelView? UserInfo { get; set; }

        // Audit
        public string? CreatedBy { get; set; }
        public DateTimeOffset CreatedTime { get; set; }
        public string? LastUpdatedBy { get; set; }
        public DateTimeOffset LastUpdatedTime { get; set; }
    }
}