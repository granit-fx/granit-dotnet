using System.Security.Cryptography;
using System.Text.Json;
using Granit.Authentication.Oidc.Internal;
using Granit.Timing;

#pragma warning disable GRSEC003 // Class handles DPoP proof tokens — required for RFC 9449 implementation

namespace Granit.Authentication.Oidc.DPoP.Internal;

/// <summary>
/// Default implementation of <see cref="IDPoPProofService"/> using EC P-256 keys
/// and manual JWT construction (no dependency on Microsoft.IdentityModel).
/// </summary>
internal sealed class DPoPProofService(IClock clock) : IDPoPProofService
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
            ["x"] = Base64Url.Encode(parameters.Q.X!),
            ["y"] = Base64Url.Encode(parameters.Q.Y!),
            ["d"] = Base64Url.Encode(parameters.D!),
        };

        return JsonSerializer.Serialize(jwk);
    }

    /// <inheritdoc/>
    public string CreateProof(string privateKeyJwk, string httpMethod, string httpUri, string? nonce = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(privateKeyJwk);
        ArgumentException.ThrowIfNullOrEmpty(httpMethod);
        ArgumentException.ThrowIfNullOrEmpty(httpUri);

        using var doc = JsonDocument.Parse(privateKeyJwk);
        JsonElement root = doc.RootElement;

        byte[] x = Base64Url.Decode(root.GetProperty("x").GetString()!);
        byte[] y = Base64Url.Decode(root.GetProperty("y").GetString()!);
        byte[] d = Base64Url.Decode(root.GetProperty("d").GetString()!);

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
            ["x"] = Base64Url.Encode(x),
            ["y"] = Base64Url.Encode(y),
        };

        // Header: {"typ":"dpop+jwt","alg":"ES256","jwk":{public key}}
        string header = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["typ"] = "dpop+jwt",
            ["alg"] = OidcConstants.Algorithms.ES256,
            ["jwk"] = publicJwk,
        });

        long now = clock.Now.ToUnixTimeSeconds();

        // Payload: {"jti":"<id>","htm":"<method>","htu":"<uri>","iat":<now>,"exp":<now+30>[,"nonce":"<nonce>"]}
#pragma warning disable GRSEC002 // jti is a cryptographic nonce, not a DB key — sequential GUIDs are not needed
        var payloadDict = new Dictionary<string, object>
        {
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["htm"] = httpMethod.ToUpperInvariant(),
            ["htu"] = htu,
            ["iat"] = now,
            ["exp"] = now + ProofLifetimeSeconds,
        };

        // Include server-provided nonce for replay protection (RFC 9449 §8)
        if (!string.IsNullOrEmpty(nonce))
        {
            payloadDict["nonce"] = nonce;
        }

        string payload = JsonSerializer.Serialize(payloadDict);
#pragma warning restore GRSEC002

        return JwtBuilder.CreateEcJwt(header, payload, ecdsa);
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
}

#pragma warning restore GRSEC003
