namespace Badminton.Core.Store
{
    public class VnPaySettings
    {
        public string MerchantCode { get; set; } = null!;
        public string SecretKey { get; set; } = null!;
        public string Endpoint { get; set; } = "https://sandbox.vnpay.io";
        public string Version { get; set; } = "2.0.0";
        public string Locale { get; set; } = "vn";
        public string BankCode { get; set; } = "SEAMLESS";
        public string CurrType { get; set; } = "VND";
        public string? CallbackUrl { get; set; }
    }
}
