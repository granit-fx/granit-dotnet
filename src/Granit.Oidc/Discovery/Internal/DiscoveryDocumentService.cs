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

        if (!string.Equals(normalizedAuthority, normalizedIssuer, StringComparison.OrdinalIgnoreCase))
        {
            throw new OidcDiscoveryException(
                authority,
                $"Issuer mismatch: expected '{normalizedAuthority}' but discovery document contains '{normalizedIssuer}'. " +
                "This may indicate a misconfigured authority URL or a security issue.");
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fetching OIDC discovery document from {DiscoveryUrl}")]
    private partial void LogFetchingDiscoveryDocument(string discoveryUrl);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC discovery document cached for authority '{Authority}' (duration: {CacheDuration})")]
    private partial void LogDiscoveryDocumentCached(string authority, TimeSpan cacheDuration);

    [LoggerMessage(Level = LogLevel.Debug, Message = "OIDC discovery document cache invalidated for authority '{Authority}'")]
    private partial void LogDiscoveryDocumentInvalidated(string authority);
}
