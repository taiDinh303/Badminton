using System.Security.Cryptography;
using System.Text;

namespace Badminton.Core.Store
{
    public static class VnPaySigner
    {
        public static string Sign(string secretKey, IReadOnlyDictionary<string, string> fields)
        {
            var sorted = fields
                .Where(kv => !string.IsNullOrEmpty(kv.Key) &&
                             kv.Key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase) &&
                             !string.IsNullOrEmpty(kv.Value) &&
                             !kv.Key.Equals("vnp_SecureHash", StringComparison.OrdinalIgnoreCase) &&
                             !kv.Key.Equals("vnp_SecureHashType", StringComparison.OrdinalIgnoreCase))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{System.Net.WebUtility.UrlEncode(kv.Key)}={System.Net.WebUtility.UrlEncode(kv.Value)}")
                .ToList();

            var signData = string.Join("&", sorted);
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signData));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public static bool Verify(string secretKey, string expectedHash,
            IReadOnlyDictionary<string, string> fields)
        {
            var computed = Sign(secretKey, fields);
            if (expectedHash.Length != computed.Length) return false;
            try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(computed), Convert.FromHexString(expectedHash)); }
            catch (FormatException) { return false; }
        }

        public static string BuildQuery(string secretKey,
            IReadOnlyDictionary<string, string> fields)
        {
            var all = new Dictionary<string, string>(fields, StringComparer.OrdinalIgnoreCase);
            all["vnp_SecureHash"] = Sign(secretKey, fields);
            return string.Join("&",
                all.Where(kv => !string.IsNullOrEmpty(kv.Value))
                  .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                  .Select(kv => $"{System.Net.WebUtility.UrlEncode(kv.Key)}={System.Net.WebUtility.UrlEncode(kv.Value)}"));
        }
    }
}
