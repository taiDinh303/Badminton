namespace Badminton.Core.Store
{
    public class MoMoSettings
    {
        public string AppId { get; set; } = null!;
        public string AccessKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = null!;
        public string Endpoint { get; set; } = "https://test-payment.momo.vn";
        public string? IpayUrl { get; set; }
    }
}
