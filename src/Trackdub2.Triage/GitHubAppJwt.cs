using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Trackdub2.Triage;

/// <summary>Creates the short-lived RS256 JWT a GitHub App uses to request installation tokens.</summary>
public static class GitHubAppJwt
{
    public static string Create(string appId, string privateKeyPem, DateTimeOffset now)
    {
        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);

        string header = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));

        // Backdated a minute for clock skew; GitHub rejects lifetimes over ten minutes.
        string payload = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iat = now.AddSeconds(-60).ToUnixTimeSeconds(),
            exp = now.AddMinutes(9).ToUnixTimeSeconds(),
            iss = appId,
        }));

        string signingInput = $"{header}.{payload}";
        byte[] signature = rsa.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{signingInput}.{Base64Url.EncodeToString(signature)}";
    }
}
