using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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

        string normalized = parsed.ToString();
        string cacheKey = CacheKeyPrefix + normalized;

        MaybeValue<GeoLocation?> cached = await _cache
            .TryGetAsync<GeoLocation?>(cacheKey, token: cancellationToken)
            .ConfigureAwait(false);
        if (cached.HasValue)
        {
            _metrics.RecordCacheHit();
            _metrics.RecordLookup(cached.Value is null ? "not_found" : "found", ipVersion);
            return cached.Value;
        }

        _metrics.RecordCacheMiss();

        long start = Stopwatch.GetTimestamp();
        GeoLocation? resolved = await QueryProvidersAsync(normalized, cancellationToken).ConfigureAwait(false);

        await _cache
            .SetAsync(cacheKey, resolved, new FusionCacheEntryOptions { Duration = _options.CacheDuration }, token: cancellationToken)
            .ConfigureAwait(false);

        string outcome = resolved is null ? "not_found" : "found";
        _metrics.RecordLookup(outcome, ipVersion);
        _metrics.RecordLookupDuration(outcome, Stopwatch.GetElapsedTime(start));
        return resolved;
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
}
