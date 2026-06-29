using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Granit.Domain.ValueObjects;
using Granit.Geocoding.Diagnostics;
using Granit.Geocoding.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Geocoding.Internal;

/// <summary>
/// Default <see cref="IReverseGeocodingService"/>: orders the registered reverse-capable providers per
/// <see cref="GranitGeocodingOptions.ProviderOrder"/>, serves and populates the result cache (long TTL for hits,
/// short TTL for misses), and chains providers until one yields an address.
/// </summary>
/// <remarks>
/// Mirrors the forward <see cref="DefaultGeocodingService"/>: provider exceptions are swallowed (logged with the
/// provider name only — <strong>never the coordinate</strong>) so a failing provider degrades to the next, and an
/// empty provider set is a silent no-op. The coordinate is hashed into the cache key so no raw location is written
/// to a shared cache.
/// </remarks>
internal sealed partial class DefaultReverseGeocodingService : IReverseGeocodingService
{
    private const string CacheKeyPrefix = "granit:geocoding:reverse:";

    private readonly List<IReverseGeocodingProvider> _providers;
    private readonly IFusionCache _cache;
    private readonly GranitGeocodingOptions _options;
    private readonly GeocodingMetrics _metrics;
    private readonly ILogger<DefaultReverseGeocodingService> _logger;

    public DefaultReverseGeocodingService(
        IEnumerable<IReverseGeocodingProvider> providers,
        IFusionCache cache,
        IOptions<GranitGeocodingOptions> options,
        GeocodingMetrics metrics,
        ILogger<DefaultReverseGeocodingService> logger)
    {
        _cache = cache;
        _options = options.Value;
        _metrics = metrics;
        _logger = logger;
        _providers = OrderProviders(providers, _options.ProviderOrder);
    }

    public async Task<ReverseGeocodingResult?> ReverseAsync(
        GeoCoordinate coordinate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coordinate);

        if (_providers.Count == 0)
        {
            LogResolutionSkipped("no enabled reverse provider");
            _metrics.RecordLookup("skipped");
            return null;
        }

        // The activity carries only the coarse result tag (never the coordinate): GDPR data-minimisation.
        using Activity? activity = GeocodingActivitySource.Source.StartActivity("geocoding.reverse");

        string cacheKey = BuildCacheKey(coordinate);

        bool resolvedFresh = false;
        ReverseGeocodingResult? resolved = await _cache.GetOrSetAsync<ReverseGeocodingResult?>(
            cacheKey,
            async (ctx, ct) =>
            {
                resolvedFresh = true;
                ReverseGeocodingResult? r = await QueryProvidersAsync(coordinate, ct).ConfigureAwait(false);
                ctx.Options.Duration = r is null ? _options.FailureCacheDuration : _options.SuccessCacheDuration;
                return r;
            },
            token: cancellationToken).ConfigureAwait(false);

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
        _metrics.RecordLookup(outcome);
        activity?.SetTag("geocoding.result", outcome);
        return resolved;
    }

    /// <summary>
    /// Hashes the coordinate (rounded to ~1&#160;m) so no raw location — personal data under GDPR — is persisted in a
    /// shared/distributed cache, where keys, unlike values, are not encrypted.
    /// </summary>
    internal static string BuildCacheKey(GeoCoordinate coordinate)
    {
        string normalized = string.Create(
            CultureInfo.InvariantCulture,
            $"{Math.Round(coordinate.Latitude, 5)},{Math.Round(coordinate.Longitude, 5)}");
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return CacheKeyPrefix + Convert.ToHexStringLower(hash);
    }

    private async Task<ReverseGeocodingResult?> QueryProvidersAsync(
        GeoCoordinate coordinate, CancellationToken cancellationToken)
    {
        foreach (IReverseGeocodingProvider provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                ReverseGeocodingResult? result =
                    await provider.ReverseAsync(coordinate, cancellationToken).ConfigureAwait(false);
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

                // GDPR: log the provider and the exception only — never the coordinate being reverse-geocoded.
                LogProviderFailed(provider.ProviderName, ex);
            }
        }

        return null;
    }

    private static List<IReverseGeocodingProvider> OrderProviders(
        IEnumerable<IReverseGeocodingProvider> providers, IList<string> order)
    {
        List<IReverseGeocodingProvider> registered = [.. providers];
        if (order.Count == 0)
        {
            return registered;
        }

        Dictionary<string, IReverseGeocodingProvider> byName = new(StringComparer.OrdinalIgnoreCase);
        foreach (IReverseGeocodingProvider provider in registered)
        {
            byName.TryAdd(provider.ProviderName, provider);
        }

        List<IReverseGeocodingProvider> ordered = [];
        foreach (string name in order)
        {
            if (byName.TryGetValue(name, out IReverseGeocodingProvider? provider))
            {
                ordered.Add(provider);
            }
        }

        return ordered;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Reverse geocoding provider '{Provider}' failed; falling back to the next provider.")]
    private partial void LogProviderFailed(string provider, Exception exception);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Reverse geocoding resolution skipped ({Reason}); returning null.")]
    private partial void LogResolutionSkipped(string reason);
}
