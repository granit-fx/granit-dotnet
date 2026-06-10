using System.Security.Cryptography;
using System.Text.Json;
using Granit.Oidc.Internal;
using Granit.Timing;

#pragma warning disable GRSEC003 // Class handles client assertions and private keys — required for private_key_jwt authentication

namespace Granit.Oidc.ClientAuthentication.Internal;

/// <summary>
/// Applies <c>private_key_jwt</c> authentication (RFC 7523) by generating a signed JWT assertion
/// using the client's private key. Supports EC (ES256) and RSA (PS256) keys.
/// </summary>
internal sealed class PrivateKeyJwtStrategy(string privateKeyJwk, IClock clock) : IClientAuthenticationStrategy
{
    private const int AssertionLifetimeSeconds = 60;

    /// <inheritdoc/>
    public void Apply(Dictionary<string, string> parameters, string clientId, string tokenEndpoint)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrEmpty(clientId);
        ArgumentException.ThrowIfNullOrEmpty(tokenEndpoint);

        string assertion = CreateAssertion(clientId, tokenEndpoint);
        parameters[OidcConstants.Parameters.ClientAssertion] = assertion;
        parameters[OidcConstants.Parameters.ClientAssertionType] = OidcConstants.ClientAssertionTypes.JwtBearer;
    }

    private string CreateAssertion(string clientId, string tokenEndpoint)
    {
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
        byte[] x = Base64Url.Decode(jwk.GetProperty("x").GetString()!);
        byte[] y = Base64Url.Decode(jwk.GetProperty("y").GetString()!);
        byte[] d = Base64Url.Decode(jwk.GetProperty("d").GetString()!);

        using var ecdsa = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = x, Y = y },
            D = d,
        });

        string header = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["typ"] = "client-authentication+jwt",
            ["alg"] = OidcConstants.Algorithms.ES256,
        });

        string payload = BuildPayload(clientId, tokenEndpoint);

        return JwtBuilder.CreateEcJwt(header, payload, ecdsa);
    }

    private string CreateRsaAssertion(JsonElement jwk, string clientId, string tokenEndpoint)
    {
        using var rsa = RSA.Create(new RSAParameters
        {
            Modulus = Base64Url.Decode(jwk.GetProperty("n").GetString()!),
            Exponent = Base64Url.Decode(jwk.GetProperty("e").GetString()!),
            D = Base64Url.Decode(jwk.GetProperty("d").GetString()!),
            P = Base64Url.Decode(jwk.GetProperty("p").GetString()!),
            Q = Base64Url.Decode(jwk.GetProperty("q").GetString()!),
            DP = Base64Url.Decode(jwk.GetProperty("dp").GetString()!),
            DQ = Base64Url.Decode(jwk.GetProperty("dq").GetString()!),
            InverseQ = Base64Url.Decode(jwk.GetProperty("qi").GetString()!),
        });

        string header = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["typ"] = "client-authentication+jwt",
            ["alg"] = OidcConstants.Algorithms.PS256,
        });

        string payload = BuildPayload(clientId, tokenEndpoint);

        return JwtBuilder.CreateRsaJwt(header, payload, rsa);
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
}

#pragma warning restore GRSEC003
