using System.Text.Json;

namespace oed_authz.Utils;

public static class JwtReader
{
    public static string? GetIssuerFromJwt(string jwt)
    {
        if (string.IsNullOrEmpty(jwt))
            return null;

        int firstDot = jwt.IndexOf('.');
        if (firstDot < 0) return null;

        int secondDot = jwt.IndexOf('.', firstDot + 1);
        if (secondDot < 0) return null;

        ReadOnlySpan<char> payload = jwt.AsSpan(firstDot + 1, secondDot - firstDot - 1);

        // A malformed token has no readable issuer. Return null so the caller rejects it instead of failing with 500.
        try
        {
            byte[] jsonBytes = Base64UrlDecode(payload);

            using var doc = JsonDocument.Parse(jsonBytes);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("iss", out var issElement)
                && issElement.ValueKind == JsonValueKind.String)
            {
                return issElement.GetString();
            }
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }

        return null;
    }

    private static byte[] Base64UrlDecode(ReadOnlySpan<char> input)
    {
        // Replace URL-friendly chars
        Span<char> buffer = stackalloc char[input.Length];
        input.CopyTo(buffer);
        buffer.Replace('-', '+');
        buffer.Replace('_', '/');

        // Add required padding
        int padding = 4 - buffer.Length % 4;
        string padded = padding < 4 ? new string(buffer) + new string('=', padding) : new string(buffer);

        return Convert.FromBase64String(padded);
    }
}
