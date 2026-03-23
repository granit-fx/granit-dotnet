using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Granit.Timing;

namespace Granit.Bff.ClientAssertion;

/// <summary>
/// Default implementation of <see cref="IBffClientAssertionService"/> using
/// manual JWT construction (no dependency on Microsoft.IdentityModel).
/// Supports EC (ES256) and RSA (PS256) keys.
/// </summary>
internal sealed class DefaultBffClientAssertionService(IClock clock) : IBffClientAssertionService
{
    private const int AssertionLifetimeSeconds = 60;

    /// <inheritdoc/>
    public string CreateAssertion(string privateKeyJwk, string clientId, string tokenEndpoint)
    {
        ArgumentException.ThrowIfNullOrEmpty(privateKeyJwk);
        ArgumentException.ThrowIfNullOrEmpty(clientId);
        ArgumentException.ThrowIfNullOrEmpty(tokenEndpoint);

        using var doc = JsonDocument.Parse(privateKeyJwk);
        JsonElement root = doc.RootElement;

        string kty = root.GetProperty("kty").GetString()!;

        return kty switch
        {
            "EC" => CreateEcAssertion(root, clientId, tokenEndpoint),
            "RSA" => CreateRsaAssertion(root, clientId, tokenEndpoint),
            _ => throw new NotSupportedException(
                $"Unsupported key type '{kty}' for private_key_jwt. Expected 'EC' or 'RSA'."),
        };
    }

    private string CreateEcAssertion(JsonElement jwk, string clientId, string tokenEndpoint)
    {
        byte[] x = Base64UrlDecode(jwk.GetProperty("x").GetString()!);
        byte[] y = Base64UrlDecode(jwk.GetProperty("y").GetString()!);
        byte[] d = Base64UrlDecode(jwk.GetProperty("d").GetString()!);

        using var ecdsa = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = x, Y = y },
            D = d,
        });

        string header = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["typ"] = "client-authentication+jwt",
            ["alg"] = "ES256",
        });

        string payload = BuildPayload(clientId, tokenEndpoint);

        string headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(header));
        string payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
        string signingInput = $"{headerB64}.{payloadB64}";

        byte[] signature = ecdsa.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $"{signingInput}.{Base64UrlEncode(signature)}";
    }

    private string CreateRsaAssertion(JsonElement jwk, string clientId, string tokenEndpoint)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(ExportRsaJwkToPkcs8Pem(jwk));

        string header = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["typ"] = "client-authentication+jwt",
            ["alg"] = "PS256",
        });

        string payload = BuildPayload(clientId, tokenEndpoint);

        string headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(header));
        string payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
        string signingInput = $"{headerB64}.{payloadB64}";

        byte[] signature = rsa.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss);

        return $"{signingInput}.{Base64UrlEncode(signature)}";
    }

    private string BuildPayload(string clientId, string tokenEndpoint)
    {
        long now = clock.Now.ToUnixTimeSeconds();

#pragma warning disable GRSEC002 // jti is a cryptographic nonce, not a DB key — sequential GUIDs are not needed
        return JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["iss"] = clientId,
            ["sub"] = clientId,
            ["aud"] = tokenEndpoint,
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["iat"] = now,
            ["exp"] = now + AssertionLifetimeSeconds,
        });
#pragma warning restore GRSEC002
    }

    private static ReadOnlySpan<char> ExportRsaJwkToPkcs8Pem(JsonElement jwk)
    {
        // Import RSA parameters from JWK fields
        using var rsa = RSA.Create(new RSAParameters
        {
            Modulus = Base64UrlDecode(jwk.GetProperty("n").GetString()!),
            Exponent = Base64UrlDecode(jwk.GetProperty("e").GetString()!),
            D = Base64UrlDecode(jwk.GetProperty("d").GetString()!),
            P = Base64UrlDecode(jwk.GetProperty("p").GetString()!),
            Q = Base64UrlDecode(jwk.GetProperty("q").GetString()!),
            DP = Base64UrlDecode(jwk.GetProperty("dp").GetString()!),
            DQ = Base64UrlDecode(jwk.GetProperty("dq").GetString()!),
            InverseQ = Base64UrlDecode(jwk.GetProperty("qi").GetString()!),
        });

        return rsa.ExportRSAPrivateKeyPem();
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    private static byte[] Base64UrlDecode(string base64Url)
    {
        string padded = base64Url
            .Replace('-', '+')
            .Replace('_', '/');

        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }
}
