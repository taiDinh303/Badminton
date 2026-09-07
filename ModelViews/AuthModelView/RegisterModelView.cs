namespace ModelViews.AuthModelView
{
    public class RegisterModelView
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;

        // OTP người dùng nhập để xác thực email
        public string? ConfirmationCode { get; set; }
    }
}
