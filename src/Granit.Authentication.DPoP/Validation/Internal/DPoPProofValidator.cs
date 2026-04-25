using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Granit.Authentication.DPoP.Diagnostics;
using Granit.Authentication.DPoP.Options;
using Granit.Timing;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Authentication.DPoP.Validation.Internal;

/// <summary>
/// Validates DPoP proof JWTs per RFC 9449 §4.3 and §7.
/// Verifies structure, signature, claims, and optionally enforces jti uniqueness.
/// </summary>
internal sealed class DPoPProofValidator(
    IOptions<DPoPValidationOptions> options,
    IClock clock,
    DPoPValidationMetrics metrics,
    IFusionCache cache) : IDPoPProofValidator
{
    private const string JtiCachePrefix = "dpop:jti:";
    private const string NonceCachePrefix = "dpop:nonce:";

    /// <summary>
    /// Private key parameters that MUST NOT appear in a DPoP proof JWK (RFC 9449 §4.1).
    /// </summary>
    private static readonly string[] PrivateKeyParameters = ["d", "p", "q", "dp", "dq", "qi", "k"];

    public async Task<DPoPValidationResult> ValidateAsync(
        string proofJwt,
        string httpMethod,
        string httpUri,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = DPoPValidationActivitySource.Source.StartActivity(DPoPValidationActivitySource.Validate);
        DPoPValidationOptions opts = options.Value;

        string[] parts = proofJwt.Split('.');
        if (parts.Length != 3)
        {
            return DPoPValidationResult.Failure("Invalid JWT structure.");
        }

        DPoPValidationResult? headerResult = ValidateHeader(parts[0], opts, out _, out string algorithm, out JsonElement jwk);
        if (headerResult is not null)
        {
            return headerResult;
        }

        // Generate a fresh nonce for the response (always, even on failure)
        string? serverNonce = opts.RequireNonce
            ? await GenerateAndStoreNonceAsync(opts, cancellationToken).ConfigureAwait(false)
            : null;

        DPoPValidationResult? payloadResult = ValidatePayload(parts[1], httpMethod, httpUri, opts, out JsonElement payload);
        if (payloadResult is not null)
        {
            return payloadResult with { ServerNonce = serverNonce };
        }

        DPoPValidationResult? nonceResult = await ValidateNonceAsync(payload, opts, serverNonce, cancellationToken).ConfigureAwait(false);
        if (nonceResult is not null)
        {
            return nonceResult;
        }

        // SECURITY: signature MUST be verified before any side effect on cache state.
        // If we recorded the jti before signature verification, an unauthenticated
        // attacker could pollute the replay cache with attacker-chosen jti values
        // (forged proofs are cheap to generate), denying legitimate clients whose
        // proofs use the same jti.
        DPoPValidationResult? signatureResult = ValidateSignature(parts, jwk, algorithm, opts);
        if (signatureResult is not null)
        {
            return signatureResult with { ServerNonce = serverNonce };
        }

        DPoPValidationResult? replayResult = await ValidateReplayProtectionAsync(payload, opts, cancellationToken).ConfigureAwait(false);
        if (replayResult is not null)
        {
            return replayResult with { ServerNonce = serverNonce };
        }

        string thumbprint = JwkThumbprintCalculator.ComputeThumbprint(jwk);
        metrics.RecordSuccess(tenantId: null);
        return DPoPValidationResult.Success(thumbprint, serverNonce);
    }

    private static DPoPValidationResult? ValidateHeader(
        string headerPart, DPoPValidationOptions opts,
        out JsonElement header, out string algorithm, out JsonElement jwk)
    {
        algorithm = string.Empty;
        jwk = default;

        try
        {
            byte[] headerBytes = Base64UrlDecode(headerPart);
            header = JsonDocument.Parse(headerBytes).RootElement;
        }
        catch (Exception)
        {
            header = default;
            return DPoPValidationResult.Failure("Invalid JWT header encoding.");
        }

        if (!header.TryGetProperty("typ", out JsonElement typ) || typ.GetString() != "dpop+jwt")
        {
            return DPoPValidationResult.Failure("Missing or invalid typ claim (expected 'dpop+jwt').");
        }

        if (!header.TryGetProperty("alg", out JsonElement alg))
        {
            return DPoPValidationResult.Failure("Missing alg claim.");
        }

        algorithm = alg.GetString()!;
        if (!opts.AllowedAlgorithms.Contains(algorithm))
        {
            return DPoPValidationResult.Failure($"Algorithm '{algorithm}' is not allowed.");
        }

        if (!header.TryGetProperty("jwk", out jwk))
        {
            return DPoPValidationResult.Failure("Missing jwk claim in header.");
        }

        // RFC 9449 §4.1: JWK MUST contain only public key parameters
        foreach (string privateParam in PrivateKeyParameters)
        {
            if (jwk.TryGetProperty(privateParam, out _))
            {
                return DPoPValidationResult.Failure(
                    $"JWK must not contain private key parameter '{privateParam}'.");
            }
        }

        return null;
    }

    private DPoPValidationResult? ValidatePayload(
        string payloadPart, string httpMethod, string httpUri,
        DPoPValidationOptions opts, out JsonElement payload)
    {
        try
        {
            byte[] payloadBytes = Base64UrlDecode(payloadPart);
            payload = JsonDocument.Parse(payloadBytes).RootElement;
        }
        catch (Exception)
        {
            payload = default;
            return DPoPValidationResult.Failure("Invalid JWT payload encoding.");
        }

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

        if (issuedAt > now + opts.ClockSkew)
        {
            return DPoPValidationResult.Failure("Proof iat is in the future.");
        }

        if (payload.TryGetProperty("exp", out JsonElement exp))
        {
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64());
            if (now > expiresAt + opts.ClockSkew)
            {
                return DPoPValidationResult.Failure("Proof has expired.");
            }
        }

        return null;
    }

    private async Task<DPoPValidationResult?> ValidateReplayProtectionAsync(
        JsonElement payload, DPoPValidationOptions opts, CancellationToken cancellationToken)
    {
        if (!opts.EnableReplayProtection)
        {
            return null;
        }

        if (!payload.TryGetProperty("jti", out JsonElement jti))
        {
            return DPoPValidationResult.Failure("Missing jti claim (required for replay protection).");
        }

        string jtiValue = jti.GetString()!;
        string jtiKey = $"{JtiCachePrefix}{jtiValue}";

        MaybeValue<bool> existing = await cache.TryGetAsync<bool>(jtiKey, token: cancellationToken).ConfigureAwait(false);
        if (existing.HasValue)
        {
            metrics.RecordReplayDetected(tenantId: null);
            return DPoPValidationResult.Failure("Proof replay detected (duplicate jti).");
        }

        await cache.SetAsync(jtiKey, true,
            new FusionCacheEntryOptions { Duration = opts.MaxProofLifetime + opts.ClockSkew },
            token: cancellationToken).ConfigureAwait(false);

        return null;
    }

    private DPoPValidationResult? ValidateSignature(string[] parts, JsonElement jwk, string algorithm, DPoPValidationOptions opts)
    {
        string kty = jwk.GetProperty("kty").GetString()!;
        byte[] signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        byte[] signature = Base64UrlDecode(parts[2]);

        bool signatureValid = kty switch
        {
            "EC" => VerifyEcSignature(jwk, signingInput, signature, algorithm),
            "RSA" => VerifyRsaSignature(jwk, signingInput, signature, algorithm, opts.MinimumRsaKeySize),
            _ => false,
        };

        if (!signatureValid)
        {
            metrics.RecordFailure("invalid_signature", tenantId: null);
            return DPoPValidationResult.Failure("Invalid proof signature.");
        }

        return null;
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

    private static bool VerifyRsaSignature(JsonElement jwk, byte[] data, byte[] signature, string algorithm, int minimumKeySizeBits)
    {
        try
        {
            byte[] n = Base64UrlDecode(jwk.GetProperty("n").GetString()!);
            byte[] e = Base64UrlDecode(jwk.GetProperty("e").GetString()!);

            // NIST SP 800-57: reject keys below the minimum size
            int keySizeBits = n.Length * 8;
            if (keySizeBits < minimumKeySizeBits)
            {
                return false;
            }

            (HashAlgorithmName hashAlg, RSASignaturePadding padding) = algorithm switch
            {
                "PS256" => (HashAlgorithmName.SHA256, RSASignaturePadding.Pss),
                "PS384" => (HashAlgorithmName.SHA384, RSASignaturePadding.Pss),
                "PS512" => (HashAlgorithmName.SHA512, RSASignaturePadding.Pss),
                "RS256" => (HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
                "RS384" => (HashAlgorithmName.SHA384, RSASignaturePadding.Pkcs1),
                "RS512" => (HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1),
                _ => (default, null!),
            };

            if (padding is null)
            {
                return false;
            }

            using var rsa = RSA.Create(new RSAParameters { Modulus = n, Exponent = e });
            return rsa.VerifyData(data, signature, hashAlg, padding);
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<string?> GenerateNonceAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.RequireNonce)
        {
            return null;
        }

        return await GenerateAndStoreNonceAsync(options.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DPoPValidationResult?> ValidateNonceAsync(
        JsonElement payload, DPoPValidationOptions opts,
        string? serverNonce, CancellationToken cancellationToken)
    {
        if (!opts.RequireNonce)
        {
            return null;
        }

        if (!payload.TryGetProperty("nonce", out JsonElement nonceClaim))
        {
            metrics.RecordFailure("missing_nonce", tenantId: null);
            return DPoPValidationResult.Failure("Missing nonce claim (required by server).", serverNonce);
        }

        string nonceValue = nonceClaim.GetString()!;
        string cacheKey = $"{NonceCachePrefix}{nonceValue}";

        MaybeValue<bool> existing = await cache.TryGetAsync<bool>(cacheKey, token: cancellationToken).ConfigureAwait(false);
        if (!existing.HasValue)
        {
            metrics.RecordFailure("invalid_nonce", tenantId: null);
            return DPoPValidationResult.Failure("Invalid or expired nonce.", serverNonce);
        }

        // Remove used nonce (one-time use)
        await cache.RemoveAsync(cacheKey, token: cancellationToken).ConfigureAwait(false);
        return null;
    }

    private async Task<string> GenerateAndStoreNonceAsync(DPoPValidationOptions opts, CancellationToken cancellationToken)
    {
        byte[] nonceBytes = RandomNumberGenerator.GetBytes(32);
        string nonce = Convert.ToBase64String(nonceBytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        string cacheKey = $"{NonceCachePrefix}{nonce}";
        await cache.SetAsync(cacheKey, true,
            new FusionCacheEntryOptions { Duration = opts.MaxProofLifetime + opts.ClockSkew },
            token: cancellationToken).ConfigureAwait(false);

        return nonce;
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
