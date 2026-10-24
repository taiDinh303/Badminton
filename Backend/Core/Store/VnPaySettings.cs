namespace Badminton.Core.Store
{
    public class VnPaySettings
    {
        public string MerchantCode { get; set; } = null!;
        public string SecretKey { get; set; } = null!;
        public string Endpoint { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
        public string Version { get; set; } = "2.1.0";
        public string ApiEndpoint { get; set; } = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";
        public string Locale { get; set; } = "vn";
        public string BankCode { get; set; } = string.Empty;
        public string CurrType { get; set; } = "VND";
        public string? CallbackUrl { get; set; }
    }
}
