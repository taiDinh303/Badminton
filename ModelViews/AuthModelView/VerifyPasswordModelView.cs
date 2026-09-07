
namespace ModelViews.AuthModelView
{
    public class VerifyPasswordModelView
    {
        public Guid UserId { get; set; }
        public string Password { get; set; } = string.Empty;
    }
}
