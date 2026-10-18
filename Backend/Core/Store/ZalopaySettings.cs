namespace Badminton.Core.Store
{
    public class ZalopaySettings
    {
        public string AppId { get; set; } = null!;
        public string ApiKey { get; set; } = null!;
        public string ApiSecret { get; set; } = null!;
        public string Endpoint { get; set; } = "https://api.zalopay.com";
        public string? CallbackUrl { get; set; }
    }
}
