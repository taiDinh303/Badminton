using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Badminton.Core.Domain;
using Badminton.Core.Store;

namespace Badminton.Tests;

internal static class PaymentCallbackFixtures
{
    public static RawWebhook MoMo(string secret, string order, int result = 0, long amount = 150000, string partner = "test")
    {
        var fields = new Dictionary<string, object> {
            ["amount"] = amount, ["extraData"] = "", ["message"] = "test", ["orderId"] = order,
            ["orderInfo"] = "Booking", ["orderType"] = "momo_wallet", ["partnerCode"] = partner,
            ["payType"] = "qr", ["requestId"] = "request-1", ["responseTime"] = 1735689600000L,
            ["resultCode"] = result, ["transId"] = 123456L
        };
        // Explicit documented order; tests do not call the production verifier to sign fixtures.
        var data = $"accessKey=audit-access&amount={amount}&extraData=&message=test&orderId={order}&orderInfo=Booking&orderType=momo_wallet&partnerCode={partner}&payType=qr&requestId=request-1&responseTime=1735689600000&resultCode={result}&transId=123456";
        fields["signature"] = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(data)));
        return new("MoMo", order, JsonSerializer.Serialize(fields), null, new Dictionary<string, string>());
    }

    public static RawWebhook Zalo(string key, string order, long amount = 150000, int app = 1234)
    {
        var data = JsonSerializer.Serialize(new { app_id = app, app_trans_id = order, amount, zp_trans_id = 123456L });
        var mac = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(data)));
        return new("Zalopay", order, JsonSerializer.Serialize(new { data, mac, type = 1 }), null, new Dictionary<string, string>());
    }

    public static RawWebhook VnPay(string key, string order, long amount = 150000, string merchant = "M00001", string status = "00")
    {
        var fields = new Dictionary<string, string> {
            ["vnp_TxnRef"] = order, ["vnp_Amount"] = (amount * 100).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["vnp_TmnCode"] = merchant, ["vnp_ResponseCode"] = status, ["vnp_TransactionStatus"] = status
        };
        return new("VnPay", order, VnPaySigner.BuildQuery(key, fields), null, new Dictionary<string, string>());
    }
}
