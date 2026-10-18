namespace Badminton.Core.Store
{
    public class MoMoSettings
    {
        public string AppId { get; set; } = null!;
        public string SecretKey { get; set; } = null!;
        public string Endpoint { get; set; } = "https://app.tapmomo.vn";
        public string? IpayUrl { get; set; }
    }
}
