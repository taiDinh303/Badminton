
namespace ModelViews.UserModelView
{
    public class SetPasswordRequest
    {
        public Guid UserId { get; set; }
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
