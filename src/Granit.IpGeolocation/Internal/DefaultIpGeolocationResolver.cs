using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Granit.Diagnostics;
using Granit.IpGeolocation.Diagnostics;
using Granit.IpGeolocation.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.IpGeolocation.Internal;

/// <summary>
/// Default <see cref="IIpGeolocationResolver"/>: orders registered providers per
/// <see cref="GranitIpGeolocationOptions.ProviderOrder"/>, short-circuits non-public addresses, serves and
/// populates the result cache, and chains providers until one yields a location.
/// </summary>
/// <remarks>
/// Provider exceptions are swallowed (logged with a masked IP) so a failing provider degrades to the next one
/// and an empty provider set is a silent no-op. The resolver therefore never throws for resolution failures.
/// </remarks>
internal sealed partial class DefaultIpGeolocationResolver : IIpGeolocationResolver
{
    private const string CacheKeyPrefix = "granit:ip_geolocation:";

    private readonly List<IIpGeolocationProvider> _providers;
    private readonly IFusionCache _cache;
    private readonly GranitIpGeolocationOptions _options;
    private readonly IpGeolocationMetrics _metrics;
    private readonly ILogger<DefaultIpGeolocationResolver> _logger;

    public DefaultIpGeolocationResolver(
        IEnumerable<IIpGeolocationProvider> providers,
        IFusionCache cache,
        IOptions<GranitIpGeolocationOptions> options,
        IpGeolocationMetrics metrics,
        ILogger<DefaultIpGeolocationResolver> logger)
    {
        _cache = cache;
        _options = options.Value;
        _metrics = metrics;
        _logger = logger;
        _providers = OrderProviders(providers, _options.ProviderOrder);

        // A plain SHA-256 cache key over the 2³² IPv4 space is fully enumerable offline: an actor with a dump of
        // a shared cache could reverse every key back to the originating IP. Warn once (this is a singleton) when
        // resolution is actually active but no HMAC secret is configured, so shared-cache deployments key the hash.
        if (_providers.Count > 0 && string.IsNullOrEmpty(_options.CacheKeySecret))
        {
            LogUnkeyedCacheKey();
        }
    }

    public async Task<GeoLocation?> ResolveAsync(string? ipAddress, CancellationToken cancellationToken = default)
    {
        if (_providers.Count == 0
            || string.IsNullOrWhiteSpace(ipAddress)
            || !IPAddress.TryParse(ipAddress, out IPAddress? parsed))
        {
            _metrics.RecordLookup("skipped", "unknown");
            return null;
        }

        string ipVersion = parsed.AddressFamily == AddressFamily.InterNetworkV6 ? "ipv6" : "ipv4";

        if (!_options.ResolvePrivateAddresses && IpAddressClassifier.IsPrivateOrReserved(parsed))
        {
            _metrics.RecordLookup("skipped", ipVersion);
            return null;
        }

        // The activity carries only coarse, non-identifying tags (never the IP): GDPR data-minimisation.
        using Activity? activity = IpGeolocationActivitySource.Source.StartActivity("ip_geolocation.resolve");
        activity?.SetTag("ip.version", ipVersion);

        string normalized = parsed.ToString();
        string cacheKey = BuildCacheKey(normalized, _options.CacheKeySecret);

        // Cache stampede protection: GetOrSetAsync invokes the factory for only ONE caller per key, so concurrent
        // lookups of the same uncached IP await that single result instead of each hitting a provider. This bounds
        // cost, third-party rate limits, and duplicate GDPR transfers of the same IP. (Coalescing is per instance;
        // across replicas each instance still resolves once — a distributed lock would be needed to coalesce
        // cluster-wide.) Negative results are cached too: the factory returns null and FusionCache stores it.
        bool resolvedFresh = false;
        GeoLocation? resolved = await _cache.GetOrSetAsync<GeoLocation?>(
            cacheKey,
            async (ctx, ct) =>
            {
                resolvedFresh = true;
                long start = Stopwatch.GetTimestamp();
                GeoLocation? r = await QueryProvidersAsync(normalized, ct).ConfigureAwait(false);
                _metrics.RecordLookupDuration(r is null ? "not_found" : "found", Stopwatch.GetElapsedTime(start));
                return r;
            },
            new FusionCacheEntryOptions { Duration = _options.CacheDuration },
            token: cancellationToken).ConfigureAwait(false);

        // A caller that did not execute the factory was served from the cache (or coalesced onto the in-flight
        // resolution) — count it as a hit; only the factory-executing caller is the real miss.
        if (resolvedFresh)
        {
            _metrics.RecordCacheMiss();
            activity?.SetTag("cache.hit", false);
        }
        else
        {
            _metrics.RecordCacheHit();
            activity?.SetTag("cache.hit", true);
        }

        string outcome = resolved is null ? "not_found" : "found";
        _metrics.RecordLookup(outcome, ipVersion);
        activity?.SetTag("ip_geolocation.result", outcome);
        return resolved;
    }

    /// <summary>
    /// Builds the cache key for <paramref name="normalizedIp"/>. The IP is hashed rather than embedded verbatim
    /// so that no raw client IP — personal data under GDPR — is persisted in a shared/distributed cache (e.g.
    /// Redis), where keys, unlike values, are not encrypted. When <paramref name="secret"/> is supplied the hash
    /// is keyed (HMAC-SHA-256), so the key cannot be brute-force-reversed to the IP from a cache dump; otherwise
    /// a plain SHA-256 is used.
    /// </summary>
    internal static string BuildCacheKey(string normalizedIp, string? secret)
    {
        byte[] input = Encoding.UTF8.GetBytes(normalizedIp);
        byte[] hash = string.IsNullOrEmpty(secret)
            ? SHA256.HashData(input)
            : HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), input);
        return CacheKeyPrefix + Convert.ToHexStringLower(hash);
    }

    private async Task<GeoLocation?> QueryProvidersAsync(string ipAddress, CancellationToken cancellationToken)
    {
        foreach (IIpGeolocationProvider provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                GeoLocation? result = await provider.ResolveAsync(ipAddress, cancellationToken).ConfigureAwait(false);
                if (result is not null)
                {
                    _metrics.RecordProviderAttempt(provider.ProviderName, "hit");
                    return result;
                }

                _metrics.RecordProviderAttempt(provider.ProviderName, "miss");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // Provider failures must degrade to the next provider, never bubble up.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _metrics.RecordProviderAttempt(provider.ProviderName, "error");
                LogProviderFailed(provider.ProviderName, LogRedaction.IpAddress(ipAddress), ex);
            }
        }

        return null;
    }

    private List<IIpGeolocationProvider> OrderProviders(
        IEnumerable<IIpGeolocationProvider> providers,
        IList<string> order)
    {
        List<IIpGeolocationProvider> registered = [.. providers];
        if (order.Count == 0)
        {
            return registered;
        }

        Dictionary<string, IIpGeolocationProvider> byName =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (IIpGeolocationProvider provider in registered)
        {
            byName.TryAdd(provider.ProviderName, provider);
        }

        List<IIpGeolocationProvider> ordered = [];
        foreach (string name in order)
        {
            if (byName.TryGetValue(name, out IIpGeolocationProvider? provider))
            {
                ordered.Add(provider);
            }
            else
            {
                LogUnknownProvider(name);
            }
        }

        return ordered;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "IP geolocation provider '{Provider}' failed for {MaskedIp}; falling back to the next provider.")]
    private partial void LogProviderFailed(string provider, string maskedIp, Exception exception);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Configured IP geolocation provider '{Provider}' is not registered and will be skipped.")]
    private partial void LogUnknownProvider(string provider);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "IP geolocation cache keys use an unkeyed SHA-256 hash. Over a shared cache this is reversible "
            + "to the originating IPv4 address from a cache dump (GDPR personal data). Set "
            + "'IpGeolocation:CacheKeySecret' (sourced from configuration/Vault, stable across instances) to key "
            + "the hash with HMAC-SHA-256.")]
    private partial void LogUnkeyedCacheKey();
}
