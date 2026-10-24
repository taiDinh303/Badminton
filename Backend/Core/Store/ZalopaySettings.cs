namespace Badminton.Core.Store
{
    public class ZalopaySettings
    {
        public string AppId { get; set; } = null!;
        public string ApiKey { get; set; } = null!;
        public string CallbackKey { get; set; } = string.Empty;
        public string ApiSecret { get; set; } = null!;
        public string Endpoint { get; set; } = "https://sb-openapi.zalopay.vn";
        public string? CallbackUrl { get; set; }
    }
}
