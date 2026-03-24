using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Granit.Authentication.DPoP.Diagnostics;
using Granit.Authentication.DPoP.Options;
using Granit.Timing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Granit.Authentication.DPoP.Validation.Internal;

/// <summary>
/// Validates DPoP proof JWTs per RFC 9449 §4.3 and §7.
/// Verifies structure, signature, claims, and optionally enforces jti uniqueness.
/// </summary>
internal sealed class DPoPProofValidator(
    IOptions<DPoPValidationOptions> options,
    IClock clock,
    DPoPValidationMetrics metrics,
    IDistributedCache? cache = null) : IDPoPProofValidator
{
    private const string JtiCachePrefix = "dpop:jti:";

    public async Task<DPoPValidationResult> ValidateAsync(
        string proofJwt,
        string httpMethod,
        string httpUri,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = DPoPValidationActivitySource.Source.StartActivity(DPoPValidationActivitySource.Validate);
        DPoPValidationOptions opts = options.Value;

        // 1. Split JWT
        string[] parts = proofJwt.Split('.');
        if (parts.Length != 3)
        {
            return DPoPValidationResult.Failure("Invalid JWT structure.");
        }

        // 2. Parse header
        JsonElement header;
        try
        {
            byte[] headerBytes = Base64UrlDecode(parts[0]);
            header = JsonDocument.Parse(headerBytes).RootElement;
        }
        catch (Exception)
        {
            return DPoPValidationResult.Failure("Invalid JWT header encoding.");
        }

        // 3. Validate header: typ, alg, jwk
        if (!header.TryGetProperty("typ", out JsonElement typ)
            || typ.GetString() != "dpop+jwt")
        {
            return DPoPValidationResult.Failure("Missing or invalid typ claim (expected 'dpop+jwt').");
        }

        if (!header.TryGetProperty("alg", out JsonElement alg))
        {
            return DPoPValidationResult.Failure("Missing alg claim.");
        }

        string algorithm = alg.GetString()!;
        if (!opts.AllowedAlgorithms.Contains(algorithm))
        {
            return DPoPValidationResult.Failure($"Algorithm '{algorithm}' is not allowed.");
        }

        if (!header.TryGetProperty("jwk", out JsonElement jwk))
        {
            return DPoPValidationResult.Failure("Missing jwk claim in header.");
        }

        // 4. Parse payload
        JsonElement payload;
        try
        {
            byte[] payloadBytes = Base64UrlDecode(parts[1]);
            payload = JsonDocument.Parse(payloadBytes).RootElement;
        }
        catch (Exception)
        {
            return DPoPValidationResult.Failure("Invalid JWT payload encoding.");
        }

        // 5. Validate payload claims
        if (!payload.TryGetProperty("htm", out JsonElement htm)
            || !string.Equals(htm.GetString(), httpMethod, StringComparison.OrdinalIgnoreCase))
        {
            return DPoPValidationResult.Failure("htm claim does not match request method.");
        }

        if (!payload.TryGetProperty("htu", out JsonElement htu))
        {
            return DPoPValidationResult.Failure("Missing htu claim.");
        }

        string normalizedRequestUri = NormalizeUri(httpUri);
        string normalizedHtu = NormalizeUri(htu.GetString()!);
        if (!string.Equals(normalizedRequestUri, normalizedHtu, StringComparison.OrdinalIgnoreCase))
        {
            return DPoPValidationResult.Failure("htu claim does not match request URI.");
        }

        DateTimeOffset now = clock.Now;

        if (!payload.TryGetProperty("iat", out JsonElement iat))
        {
            return DPoPValidationResult.Failure("Missing iat claim.");
        }

        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(iat.GetInt64());
        if (now - issuedAt > opts.MaxProofLifetime)
        {
            return DPoPValidationResult.Failure("Proof is too old (iat).");
        }

        if (payload.TryGetProperty("exp", out JsonElement exp))
        {
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64());
            if (now > expiresAt + opts.ClockSkew)
            {
                return DPoPValidationResult.Failure("Proof has expired.");
            }
        }

        // 6. jti replay protection
        if (opts.EnableReplayProtection && cache is not null)
        {
            if (!payload.TryGetProperty("jti", out JsonElement jti))
            {
                return DPoPValidationResult.Failure("Missing jti claim (required for replay protection).");
            }

            string jtiValue = jti.GetString()!;
            string jtiKey = $"{JtiCachePrefix}{jtiValue}";

            byte[]? existing = await cache.GetAsync(jtiKey, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                metrics.RecordReplayDetected(tenantId: null);
                return DPoPValidationResult.Failure("Proof replay detected (duplicate jti).");
            }

            await cache.SetAsync(jtiKey, "1"u8.ToArray(),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = opts.MaxProofLifetime + opts.ClockSkew,
                },
                cancellationToken).ConfigureAwait(false);
        }

        // 7. Verify signature
        string kty = jwk.GetProperty("kty").GetString()!;
        byte[] signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        byte[] signature = Base64UrlDecode(parts[2]);

        bool signatureValid = kty switch
        {
            "EC" => VerifyEcSignature(jwk, signingInput, signature, algorithm),
            "RSA" => VerifyRsaSignature(jwk, signingInput, signature),
            _ => false,
        };

        if (!signatureValid)
        {
            metrics.RecordFailure("invalid_signature", tenantId: null);
            return DPoPValidationResult.Failure("Invalid proof signature.");
        }

        // 8. Compute JWK Thumbprint (RFC 7638)
        string thumbprint = JwkThumbprintCalculator.ComputeThumbprint(jwk);

        metrics.RecordSuccess(tenantId: null);
        return DPoPValidationResult.Success(thumbprint);
    }

    private static bool VerifyEcSignature(JsonElement jwk, byte[] data, byte[] signature, string algorithm)
    {
        try
        {
            byte[] x = Base64UrlDecode(jwk.GetProperty("x").GetString()!);
            byte[] y = Base64UrlDecode(jwk.GetProperty("y").GetString()!);

            string curveName = jwk.TryGetProperty("crv", out JsonElement crv) ? crv.GetString()! : "P-256";
            ECCurve curve = curveName switch
            {
                "P-256" => ECCurve.NamedCurves.nistP256,
                "P-384" => ECCurve.NamedCurves.nistP384,
                "P-521" => ECCurve.NamedCurves.nistP521,
                _ => throw new NotSupportedException($"Unsupported curve '{curveName}'."),
            };

            HashAlgorithmName hashAlg = algorithm switch
            {
                "ES256" => HashAlgorithmName.SHA256,
                "ES384" => HashAlgorithmName.SHA384,
                "ES512" => HashAlgorithmName.SHA512,
                _ => HashAlgorithmName.SHA256,
            };

            using var ecdsa = ECDsa.Create(new ECParameters
            {
                Curve = curve,
                Q = new ECPoint { X = x, Y = y },
            });

            return ecdsa.VerifyData(data, signature, hashAlg, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch
        {
            return false;
        }
    }

    private static bool VerifyRsaSignature(JsonElement jwk, byte[] data, byte[] signature)
    {
        try
        {
            byte[] n = Base64UrlDecode(jwk.GetProperty("n").GetString()!);
            byte[] e = Base64UrlDecode(jwk.GetProperty("e").GetString()!);

            using var rsa = RSA.Create(new RSAParameters { Modulus = n, Exponent = e });
            return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Normalizes a URI for htu comparison: lowercase scheme+host, strip query/fragment,
    /// remove default ports, remove trailing slash.
    /// </summary>
    private static string NormalizeUri(string uri)
    {
        if (Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed))
        {
            // GetLeftPart(Path) gives scheme + host + path, strips query + fragment
            string normalized = parsed.GetLeftPart(UriPartial.Path);
            return normalized.TrimEnd('/');
        }

        // Fallback: strip query/fragment manually
        int queryIdx = uri.IndexOf('?');
        if (queryIdx >= 0)
        {
            uri = uri[..queryIdx];
        }

        int fragIdx = uri.IndexOf('#');
        if (fragIdx >= 0)
        {
            uri = uri[..fragIdx];
        }

        return uri.TrimEnd('/');
    }

    private static byte[] Base64UrlDecode(string base64Url)
    {
        string padded = base64Url.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }
}
