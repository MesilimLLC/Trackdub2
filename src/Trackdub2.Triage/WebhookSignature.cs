using System.Security.Cryptography;
using System.Text;

namespace Trackdub2.Triage;

/// <summary>Verifies GitHub's X-Hub-Signature-256 header (HMAC-SHA256 of the raw body).</summary>
public static class WebhookSignature
{
    private const string Prefix = "sha256=";

    public static bool IsValid(string secret, ReadOnlySpan<byte> body, string? signatureHeader)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(signatureHeader) ||
            !signatureHeader.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        byte[] actual;
        try
        {
            actual = Convert.FromHexString(signatureHeader.AsSpan(Prefix.Length));
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
