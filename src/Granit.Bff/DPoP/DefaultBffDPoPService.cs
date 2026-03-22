using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Granit.Timing;

namespace Granit.Bff.DPoP;

/// <summary>
/// Default implementation of <see cref="IBffDPoPService"/> using EC P-256 keys
/// and manual JWT construction (no dependency on Microsoft.IdentityModel).
/// </summary>
internal sealed class DefaultBffDPoPService(IClock clock) : IBffDPoPService
{
    private const int ProofLifetimeSeconds = 30;

    /// <inheritdoc/>
    public string GenerateKeyPair()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ECParameters parameters = ecdsa.ExportParameters(includePrivateParameters: true);

        var jwk = new Dictionary<string, string>
        {
            ["kty"] = "EC",
            ["crv"] = "P-256",
            ["x"] = Base64UrlEncode(parameters.Q.X!),
            ["y"] = Base64UrlEncode(parameters.Q.Y!),
            ["d"] = Base64UrlEncode(parameters.D!),
        };

        return JsonSerializer.Serialize(jwk);
    }

    /// <inheritdoc/>
    public string CreateProof(string privateKeyJwk, string httpMethod, string httpUri)
    {
        ArgumentException.ThrowIfNullOrEmpty(privateKeyJwk);
        ArgumentException.ThrowIfNullOrEmpty(httpMethod);
        ArgumentException.ThrowIfNullOrEmpty(httpUri);

        using var doc = JsonDocument.Parse(privateKeyJwk);
        JsonElement root = doc.RootElement;

        byte[] x = Base64UrlDecode(root.GetProperty("x").GetString()!);
        byte[] y = Base64UrlDecode(root.GetProperty("y").GetString()!);
        byte[] d = Base64UrlDecode(root.GetProperty("d").GetString()!);

        using var ecdsa = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = x, Y = y },
            D = d,
        });

        // Strip query string and fragment from URI (RFC 9449 §4.3)
        string htu = StripQueryAndFragment(httpUri);

        // Build public key JWK for header (without private key "d")
        var publicJwk = new Dictionary<string, string>
        {
            ["kty"] = "EC",
            ["crv"] = "P-256",
            ["x"] = Base64UrlEncode(x),
            ["y"] = Base64UrlEncode(y),
        };

        // Header: {"typ":"dpop+jwt","alg":"ES256","jwk":{public key}}
        string header = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["typ"] = "dpop+jwt",
            ["alg"] = "ES256",
            ["jwk"] = publicJwk,
        });

        long now = clock.Now.ToUnixTimeSeconds();

        // Payload: {"jti":"<nonce>","htm":"<method>","htu":"<uri>","iat":<now>,"exp":<now+30>}
#pragma warning disable GRSEC002 // jti is a cryptographic nonce, not a DB key — sequential GUIDs are not needed
        string payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["htm"] = httpMethod.ToUpperInvariant(),
            ["htu"] = htu,
            ["iat"] = now,
            ["exp"] = now + ProofLifetimeSeconds,
        });
#pragma warning restore GRSEC002

        string headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(header));
        string payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));

        string signingInput = $"{headerB64}.{payloadB64}";
        byte[] signature = ecdsa.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $"{signingInput}.{Base64UrlEncode(signature)}";
    }

    private static string StripQueryAndFragment(string uri)
    {
        int queryIndex = uri.IndexOf('?');
        if (queryIndex >= 0)
        {
            return uri[..queryIndex];
        }

        int fragmentIndex = uri.IndexOf('#');
        if (fragmentIndex >= 0)
        {
            return uri[..fragmentIndex];
        }

        return uri;
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
