using System.Text.Json;
using Granit.Oidc.Exceptions;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Oidc.Discovery.Internal;

/// <summary>
/// Default implementation of <see cref="IDiscoveryDocumentService"/> that fetches, validates,
/// and caches OIDC discovery documents with per-authority concurrency control.
/// </summary>
internal sealed partial class DiscoveryDocumentService(
    IHttpClientFactory httpClientFactory,
    IFusionCache cache,
    ILogger<DiscoveryDocumentService> logger) : IDiscoveryDocumentService
{
    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromHours(24);

    // Base Granit.Oidc cannot reference Granit.Http.Resilience (layering), so the
    // discovery client has no resilience pipeline. Cap its timeout well below the
    // 100s HttpClient default so a slow or unreachable authority fails fast instead
    // of stalling every token acquisition queued behind it.
    private static readonly TimeSpan DiscoveryTimeout = TimeSpan.FromSeconds(15);

    /// <inheritdoc/>
    public async Task<OidcDiscoveryDocument> GetAsync(string authority, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(authority);

        string cacheKey = $"oidc:discovery:{authority}";

        return await cache.GetOrSetAsync<OidcDiscoveryDocument>(
            cacheKey,
            async (ctx, ct) =>
            {
                string discoveryUrl = $"{authority.TrimEnd('/')}/.well-known/openid-configuration";
                LogFetchingDiscoveryDocument(discoveryUrl);

                using HttpClient httpClient = httpClientFactory.CreateClient(nameof(DiscoveryDocumentService));
                httpClient.Timeout = DiscoveryTimeout;
                using HttpResponseMessage response = await httpClient.GetAsync(discoveryUrl, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                using Stream stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

                var discoveryDocument = OidcDiscoveryDocument.FromJson(doc.RootElement);

                ValidateIssuer(authority, discoveryDocument.Issuer);

                LogDiscoveryDocumentCached(authority, DefaultCacheDuration);

                return discoveryDocument;
            },
            new FusionCacheEntryOptions { Duration = DefaultCacheDuration },
            token: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void InvalidateCache(string authority)
    {
        ArgumentException.ThrowIfNullOrEmpty(authority);

        cache.Remove($"oidc:discovery:{authority}");
        LogDiscoveryDocumentInvalidated(authority);
    }

    private static void ValidateIssuer(string authority, string issuer)
    {
        // Normalize: strip trailing slash for comparison
        string normalizedAuthority = authority.TrimEnd('/');
        string normalizedIssuer = issuer.TrimEnd('/');

        if (!IssuerMatches(normalizedAuthority, normalizedIssuer))
        {
            throw new OidcDiscoveryException(
                authority,
                $"Issuer mismatch: expected '{normalizedAuthority}' but discovery document contains '{normalizedIssuer}'. " +
                "This may indicate a misconfigured authority URL or a security issue.");
        }
    }

    // OIDC Discovery §4.3 requires the issuer to be identical to the authority used.
    // Comparison is case-sensitive on the path (per URI semantics) but tolerates
    // case differences in scheme and host, which are case-insensitive by RFC 3986.
    private static bool IssuerMatches(string authority, string issuer)
    {
        if (string.Equals(authority, issuer, StringComparison.Ordinal))
        {
            return true;
        }

        return Uri.TryCreate(authority, UriKind.Absolute, out Uri? a)
            && Uri.TryCreate(issuer, UriKind.Absolute, out Uri? i)
            && string.Equals(a.Scheme, i.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.Host, i.Host, StringComparison.OrdinalIgnoreCase)
            && a.Port == i.Port
            && string.Equals(a.AbsolutePath.TrimEnd('/'), i.AbsolutePath.TrimEnd('/'), StringComparison.Ordinal);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fetching OIDC discovery document from {DiscoveryUrl}")]
    private partial void LogFetchingDiscoveryDocument(string discoveryUrl);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC discovery document cached for authority '{Authority}' (duration: {CacheDuration})")]
    private partial void LogDiscoveryDocumentCached(string authority, TimeSpan cacheDuration);

    [LoggerMessage(Level = LogLevel.Debug, Message = "OIDC discovery document cache invalidated for authority '{Authority}'")]
    private partial void LogDiscoveryDocumentInvalidated(string authority);
}
