using System.Collections.Concurrent;
using System.Text.Json;
using Granit.Oidc.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Granit.Oidc.Discovery.Internal;

/// <summary>
/// Default implementation of <see cref="IDiscoveryDocumentService"/> that fetches, validates,
/// and caches OIDC discovery documents with per-authority concurrency control.
/// </summary>
internal sealed partial class DiscoveryDocumentService(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    ILogger<DiscoveryDocumentService> logger) : IDiscoveryDocumentService
{
    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromHours(24);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();

    /// <inheritdoc/>
    public async Task<OidcDiscoveryDocument> GetAsync(string authority, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(authority);

        string cacheKey = $"oidc:discovery:{authority}";

        if (cache.TryGetValue(cacheKey, out OidcDiscoveryDocument? cached))
        {
            return cached!;
        }

        SemaphoreSlim semaphore = _semaphores.GetOrAdd(authority, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Double-check after acquiring semaphore
            if (cache.TryGetValue(cacheKey, out cached))
            {
                return cached!;
            }

            string discoveryUrl = $"{authority.TrimEnd('/')}/.well-known/openid-configuration";
            LogFetchingDiscoveryDocument(discoveryUrl);

            using HttpClient httpClient = httpClientFactory.CreateClient(nameof(DiscoveryDocumentService));
            using HttpResponseMessage response = await httpClient.GetAsync(discoveryUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            var discoveryDocument = OidcDiscoveryDocument.FromJson(doc.RootElement);

            // Validate issuer matches authority (security check per OpenID Connect Discovery §4.3)
            ValidateIssuer(authority, discoveryDocument.Issuer);

            cache.Set(cacheKey, discoveryDocument, DefaultCacheDuration);
            LogDiscoveryDocumentCached(authority, DefaultCacheDuration);

            return discoveryDocument;
        }
        catch (HttpRequestException ex)
        {
            throw new OidcDiscoveryException(authority, $"HTTP request failed: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            throw new OidcDiscoveryException(authority, $"Invalid JSON in discovery document: {ex.Message}", ex);
        }
        catch (KeyNotFoundException ex)
        {
            throw new OidcDiscoveryException(authority, $"Missing required field in discovery document: {ex.Message}", ex);
        }
        finally
        {
            semaphore.Release();
        }
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
