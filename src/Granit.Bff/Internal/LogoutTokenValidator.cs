using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Granit.Bff.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Bff.Internal;

/// <summary>
/// Validates OIDC Back-Channel Logout tokens by verifying the JWT signature
/// against the IdP's JWKS endpoint (auto-discovered from OIDC configuration).
/// JWKS keys are cached in <see cref="IFusionCache"/> with automatic re-fetch
/// on unknown <c>kid</c> (handles key rotation).
/// </summary>
internal sealed partial class LogoutTokenValidator(
    IOptions<GranitBffOptions> options,
    IHttpClientFactory httpClientFactory,
    IFusionCache cache,
    ILogger<LogoutTokenValidator> logger) : ILogoutTokenValidator
{
    private const string JwksCacheKey = "bff:oidc-jwks";
    private static readonly TimeSpan JwksCacheDuration = TimeSpan.FromHours(1);

    public async Task<ValidatedLogoutToken?> ValidateAsync(
        string logoutToken, string expectedClientId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logoutToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedClientId);

        string[] parts = logoutToken.Split('.');
        if (parts.Length != 3)
        {
            LogInvalidStructure(logger);
            return null;
        }

        JwtHeader? header = ParseJwtHeader(parts[0]);
        if (header is null)
        {
            LogInvalidHeader(logger);
            return null;
        }

        // Reject alg:none (critical — CWE-345)
        if (string.Equals(header.Algorithm, "none", StringComparison.OrdinalIgnoreCase))
        {
            LogAlgNoneRejected(logger);
            return null;
        }

        // Fetch JWKS and find matching key
        JsonElement? jwk = await FindSigningKeyAsync(header.KeyId, cancellationToken)
            .ConfigureAwait(false);

        if (jwk is null)
        {
            LogKeyNotFound(logger, header.KeyId ?? "(null)");
            return null;
        }

        // Verify signature
        byte[] signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        byte[] signature = Base64UrlDecode(parts[2]);

        if (!VerifySignature(header.Algorithm, jwk.Value, signingInput, signature))
        {
            LogSignatureInvalid(logger);
            return null;
        }

        // Parse and validate claims
        ValidatedLogoutToken? claims = ParsePayload(parts[1]);
        if (claims is null)
        {
            LogInvalidPayload(logger);
            return null;
        }

        // Validate issuer
        string expectedIssuer = options.Value.Authority.ToString().TrimEnd('/');
        if (!string.Equals(claims.Issuer?.TrimEnd('/'), expectedIssuer, StringComparison.OrdinalIgnoreCase))
        {
            LogIssuerMismatch(logger, claims.Issuer ?? "(null)", expectedIssuer);
            return null;
        }

        // Validate audience (OIDC Back-Channel Logout §2.4 — aud MUST contain client_id)
        if (claims.Audiences is not null
            && !claims.Audiences.Contains(expectedClientId, StringComparer.Ordinal))
        {
            LogAudienceMismatch(logger, expectedClientId);
            return null;
        }

        // Validate back-channel logout event
        if (!claims.HasBackChannelLogoutEvent)
        {
            LogMissingEvent(logger);
            return null;
        }

        return claims;
    }

    private async Task<JsonElement?> FindSigningKeyAsync(string? kid, CancellationToken ct)
    {
        List<JsonElement>? keys = await GetJwksAsync(ct).ConfigureAwait(false);
        if (keys is null || keys.Count == 0)
        {
            return null;
        }

        if (string.IsNullOrEmpty(kid))
        {
            return keys.Find(k =>
                !k.TryGetProperty("use", out JsonElement use) || use.GetString() == "sig");
        }

        JsonElement? match = keys.Find(k =>
            k.TryGetProperty("kid", out JsonElement kidProp) && kidProp.GetString() == kid);

        if (match is null)
        {
            // kid not found — re-fetch JWKS (key rotation may have occurred)
            await cache.RemoveAsync(JwksCacheKey, token: ct).ConfigureAwait(false);
            keys = await GetJwksAsync(ct).ConfigureAwait(false);
            match = keys?.Find(k =>
                k.TryGetProperty("kid", out JsonElement kidProp) && kidProp.GetString() == kid);
        }

        return match;
    }

    private async Task<List<JsonElement>?> GetJwksAsync(CancellationToken ct)
    {
        return await cache.GetOrSetAsync<List<JsonElement>>(
            JwksCacheKey,
            async (_, token) => await FetchJwksFromAuthorityAsync(token).ConfigureAwait(false),
            new FusionCacheEntryOptions { Duration = JwksCacheDuration },
            token: ct).ConfigureAwait(false);
    }

    private async Task<List<JsonElement>> FetchJwksFromAuthorityAsync(CancellationToken ct)
    {
        using HttpClient client = httpClientFactory.CreateClient("Granit.Bff");
        string authorityBase = options.Value.Authority.ToString().TrimEnd('/');

        // Fetch OIDC discovery document
        string discoveryUrl = $"{authorityBase}/.well-known/openid-configuration";
        using HttpResponseMessage discoveryResponse = await client
            .GetAsync(discoveryUrl, ct).ConfigureAwait(false);
        discoveryResponse.EnsureSuccessStatusCode();

        using Stream discoveryStream = await discoveryResponse.Content
            .ReadAsStreamAsync(ct).ConfigureAwait(false);
        using JsonDocument discoveryDoc = await JsonDocument
            .ParseAsync(discoveryStream, cancellationToken: ct).ConfigureAwait(false);

        string? jwksUri = discoveryDoc.RootElement.TryGetProperty("jwks_uri", out JsonElement jwksUriEl)
            ? jwksUriEl.GetString() : null;

        if (string.IsNullOrEmpty(jwksUri))
        {
            LogJwksUriMissing(logger);
            return [];
        }

        // Fetch JWKS
        using HttpResponseMessage jwksResponse = await client
            .GetAsync(jwksUri, ct).ConfigureAwait(false);
        jwksResponse.EnsureSuccessStatusCode();

        using Stream jwksStream = await jwksResponse.Content
            .ReadAsStreamAsync(ct).ConfigureAwait(false);
        using JsonDocument jwksDoc = await JsonDocument
            .ParseAsync(jwksStream, cancellationToken: ct).ConfigureAwait(false);

        if (!jwksDoc.RootElement.TryGetProperty("keys", out JsonElement keysArray)
            || keysArray.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        // Clone elements so they survive document disposal
        List<JsonElement> keys = [];
        foreach (JsonElement key in keysArray.EnumerateArray())
        {
            keys.Add(key.Clone());
        }

        LogJwksFetched(logger, keys.Count);
        return keys;
    }

    private static bool VerifySignature(
        string algorithm, JsonElement jwk, byte[] signingInput, byte[] signature)
    {
        string? kty = jwk.TryGetProperty("kty", out JsonElement ktyProp) ? ktyProp.GetString() : null;

        return (algorithm.ToUpperInvariant(), kty) switch
        {
            ("RS256", "RSA") => VerifyRsa(jwk, signingInput, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
            ("RS384", "RSA") => VerifyRsa(jwk, signingInput, signature, HashAlgorithmName.SHA384, RSASignaturePadding.Pkcs1),
            ("RS512", "RSA") => VerifyRsa(jwk, signingInput, signature, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1),
            ("PS256", "RSA") => VerifyRsa(jwk, signingInput, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss),
            ("PS384", "RSA") => VerifyRsa(jwk, signingInput, signature, HashAlgorithmName.SHA384, RSASignaturePadding.Pss),
            ("PS512", "RSA") => VerifyRsa(jwk, signingInput, signature, HashAlgorithmName.SHA512, RSASignaturePadding.Pss),
            ("ES256", "EC") => VerifyEc(jwk, signingInput, signature, HashAlgorithmName.SHA256, ECCurve.NamedCurves.nistP256),
            ("ES384", "EC") => VerifyEc(jwk, signingInput, signature, HashAlgorithmName.SHA384, ECCurve.NamedCurves.nistP384),
            ("ES512", "EC") => VerifyEc(jwk, signingInput, signature, HashAlgorithmName.SHA512, ECCurve.NamedCurves.nistP521),
            _ => false,
        };
    }

    private static bool VerifyRsa(
        JsonElement jwk, byte[] signingInput, byte[] signature,
        HashAlgorithmName hashAlgorithm, RSASignaturePadding padding)
    {
        try
        {
            string? n = jwk.GetProperty("n").GetString();
            string? e = jwk.GetProperty("e").GetString();
            if (n is null || e is null)
            {
                return false;
            }

            using var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters
            {
                Modulus = Base64UrlDecode(n),
                Exponent = Base64UrlDecode(e),
            });

            return rsa.VerifyData(signingInput, signature, hashAlgorithm, padding);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static bool VerifyEc(
        JsonElement jwk, byte[] signingInput, byte[] signature,
        HashAlgorithmName hashAlgorithm, ECCurve curve)
    {
        try
        {
            string? x = jwk.GetProperty("x").GetString();
            string? y = jwk.GetProperty("y").GetString();
            if (x is null || y is null)
            {
                return false;
            }

            using var ecdsa = ECDsa.Create(new ECParameters
            {
                Curve = curve,
                Q = new ECPoint
                {
                    X = Base64UrlDecode(x),
                    Y = Base64UrlDecode(y),
                },
            });

            return ecdsa.VerifyData(
                signingInput, signature, hashAlgorithm,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static JwtHeader? ParseJwtHeader(string headerBase64Url)
    {
        try
        {
            byte[] bytes = Base64UrlDecode(headerBase64Url);
            using var doc = JsonDocument.Parse(bytes);

            string? alg = doc.RootElement.TryGetProperty("alg", out JsonElement algProp)
                ? algProp.GetString() : null;
            string? kid = doc.RootElement.TryGetProperty("kid", out JsonElement kidProp)
                ? kidProp.GetString() : null;

            return alg is not null ? new JwtHeader(alg, kid) : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }

    private static ValidatedLogoutToken? ParsePayload(string payloadBase64Url)
    {
        try
        {
            byte[] bytes = Base64UrlDecode(payloadBase64Url);
            using var doc = JsonDocument.Parse(bytes);
            JsonElement root = doc.RootElement;

            string? issuer = root.TryGetProperty("iss", out JsonElement iss) ? iss.GetString() : null;
            string? subject = root.TryGetProperty("sub", out JsonElement sub) ? sub.GetString() : null;
            string? jti = root.TryGetProperty("jti", out JsonElement jtiEl) ? jtiEl.GetString() : null;

            // Parse audience (can be string or array per JWT spec)
            List<string>? audiences = null;
            if (root.TryGetProperty("aud", out JsonElement aud))
            {
                audiences = aud.ValueKind == JsonValueKind.Array
                    ? [.. aud.EnumerateArray().Select(a => a.GetString()!).Where(a => a is not null)]
                    : aud.GetString() is { } audStr ? [audStr] : null;
            }

            bool hasEvent = root.TryGetProperty("events", out JsonElement events)
                && events.TryGetProperty("http://schemas.openid.net/event/backchannel-logout", out _);

            return new ValidatedLogoutToken(issuer, subject, jti, hasEvent) { Audiences = audiences };
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        string base64 = input.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        return Convert.FromBase64String(base64);
    }

    private sealed record JwtHeader(string Algorithm, string? KeyId);

    // ──── Source-generated log messages ────

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF logout token: invalid JWT structure (expected 3 parts)")]
    private static partial void LogInvalidStructure(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF logout token: invalid or missing JWT header")]
    private static partial void LogInvalidHeader(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF logout token: alg:none rejected — unsigned tokens are not accepted")]
    private static partial void LogAlgNoneRejected(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF logout token: signing key not found for kid '{KeyId}'")]
    private static partial void LogKeyNotFound(ILogger logger, string keyId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF logout token: signature verification failed")]
    private static partial void LogSignatureInvalid(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF logout token: invalid JWT payload")]
    private static partial void LogInvalidPayload(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF logout token: issuer mismatch — received '{ReceivedIssuer}', expected '{ExpectedIssuer}'")]
    private static partial void LogIssuerMismatch(ILogger logger, string receivedIssuer, string expectedIssuer);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF logout token: audience does not contain expected client_id '{ExpectedClientId}'")]
    private static partial void LogAudienceMismatch(ILogger logger, string expectedClientId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF logout token: missing http://schemas.openid.net/event/backchannel-logout event")]
    private static partial void LogMissingEvent(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF logout token: OIDC discovery document missing jwks_uri")]
    private static partial void LogJwksUriMissing(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "BFF logout token: fetched {Count} JWKS signing key(s) from authority")]
    private static partial void LogJwksFetched(ILogger logger, int count);
}
