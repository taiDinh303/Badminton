namespace ModelViews.Auth
{
    public class LoginResponse
    {
        public string AccessToken { get; set; } = null!;

        public int UserId { get; set; }

        public string FullName { get; set; } = null!;

        public string Email { get; set; } = null!;

        public string Role { get; set; } = null!;
    }
}
