using System.Security.Cryptography;
using System.Text.Json;

namespace Services;

internal static class WebhookSecurity
{
    public static bool EqualHex(string? supplied, string expected)
    {
        if (supplied is null || supplied.Length != expected.Length) return false;
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(supplied), Convert.FromHexString(expected)); }
        catch (FormatException) { return false; }
    }

    public static Dictionary<string, string> ReadObject(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in doc.RootElement.EnumerateObject())
        {
            if (!seen.Add(p.Name)) throw new JsonException();
            if (p.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number)) continue;
            if (!values.TryAdd(p.Name, p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText())) throw new JsonException();
        }
        return values;
    }
}
