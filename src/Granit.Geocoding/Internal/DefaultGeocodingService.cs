using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Granit.Geocoding.Diagnostics;
using Granit.Geocoding.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Geocoding.Internal;

/// <summary>
/// Default <see cref="IGeocodingService"/>: orders registered providers per
/// <see cref="GranitGeocodingOptions.ProviderOrder"/>, normalises the address into a stable cache key, serves and
/// populates the result cache (long TTL for hits, short TTL for misses), and chains providers until one yields a
/// coordinate.
/// </summary>
/// <remarks>
/// Provider exceptions are swallowed (logged with the provider name only — <strong>never the address</strong>) so a
/// failing provider degrades to the next one and an empty provider set is a silent no-op. The service therefore
/// never throws for resolution failures, and no address value is ever written to a log, trace tag, or cache key.
/// </remarks>
internal sealed partial class DefaultGeocodingService : IGeocodingService
{
    private const string CacheKeyPrefix = "granit:geocoding:";

    private readonly List<IGeocodingProvider> _providers;
    private readonly IFusionCache _cache;
    private readonly GranitGeocodingOptions _options;
    private readonly GeocodingMetrics _metrics;
    private readonly ILogger<DefaultGeocodingService> _logger;

    public DefaultGeocodingService(
        IEnumerable<IGeocodingProvider> providers,
        IFusionCache cache,
        IOptions<GranitGeocodingOptions> options,
        GeocodingMetrics metrics,
        ILogger<DefaultGeocodingService> logger)
    {
        _cache = cache;
        _options = options.Value;
        _metrics = metrics;
        _logger = logger;
        _providers = OrderProviders(providers, _options.ProviderOrder);
    }

    public async Task<GeocodingResult?> GeocodeAsync(PostalAddress address, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);

        // Nothing to geocode without at least a locality or a country, and a no-op when no provider is enabled.
        if (_providers.Count == 0
            || (string.IsNullOrWhiteSpace(address.Locality) && string.IsNullOrWhiteSpace(address.Country)))
        {
            // GDPR: log the coarse reason only — never the address.
            LogResolutionSkipped(_providers.Count == 0 ? "no enabled provider" : "address not geocodable");
            _metrics.RecordLookup("skipped");
            return null;
        }

        // The activity carries only the coarse result tag (never the address): GDPR data-minimisation.
        using Activity? activity = GeocodingActivitySource.Source.StartActivity("geocoding.resolve");

        string cacheKey = BuildCacheKey(NormalizeKey(address));

        // Cache stampede protection: GetOrSetAsync invokes the factory for only ONE caller per key, so concurrent
        // lookups of the same uncached address await that single result instead of each hitting a provider. This
        // bounds cost, third-party rate limits, and duplicate GDPR transfers. Negative results are cached too —
        // with the shorter FailureCacheDuration applied adaptively inside the factory.
        // The flag lives in a heap holder so the factory closure can signal back whether it actually executed; a
        // plain captured local reads as never-assigned to static flow analysis (the delegate's run isn't provable).
        StrongBox<bool> resolvedFresh = new(false);
        GeocodingResult? resolved = await _cache.GetOrSetAsync<GeocodingResult?>(
            cacheKey,
            async (ctx, ct) =>
            {
                resolvedFresh.Value = true;
                long start = Stopwatch.GetTimestamp();
                GeocodingResult? r = await QueryProvidersAsync(address, ct).ConfigureAwait(false);
                ctx.Options.Duration = r is null ? _options.FailureCacheDuration : _options.SuccessCacheDuration;
                _metrics.RecordLookupDuration(r is null ? "not_found" : "found", Stopwatch.GetElapsedTime(start));
                return r;
            },
            token: cancellationToken).ConfigureAwait(false);

        // A caller that did not execute the factory was served from the cache (or coalesced onto the in-flight
        // resolution) — count it as a hit; only the factory-executing caller is the real miss.
        if (resolvedFresh.Value)
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
    /// Builds the normalised lookup key for <paramref name="address"/>: each component is trimmed, lower-cased
    /// (invariant) and has runs of internal whitespace collapsed to a single space, then joined as
    /// <c>"Street|PostalCode|Locality|Country"</c>. Normalisation means trivially different spellings of the same
    /// address (casing, padding) share one cache entry and one provider call.
    /// </summary>
    internal static string NormalizeKey(PostalAddress address) =>
        string.Join('|',
            NormalizeComponent(address.Street),
            NormalizeComponent(address.PostalCode),
            NormalizeComponent(address.Locality),
            NormalizeComponent(address.Country));

    /// <summary>
    /// Hashes the normalised key so that no raw address — personal data under GDPR — is persisted in a
    /// shared/distributed cache (e.g. Redis), where keys, unlike values, are not encrypted.
    /// </summary>
    internal static string BuildCacheKey(string normalizedKey)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedKey));
        return CacheKeyPrefix + Convert.ToHexStringLower(hash);
    }

    private static string NormalizeComponent(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : WhitespaceRegex().Replace(value.Trim(), " ").ToLower(CultureInfo.InvariantCulture);

    private async Task<GeocodingResult?> QueryProvidersAsync(PostalAddress address, CancellationToken cancellationToken)
    {
        foreach (IGeocodingProvider provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                GeocodingResult? result = await provider.ResolveAsync(address, cancellationToken).ConfigureAwait(false);
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

                // GDPR: log the provider and the exception only — never the address being geocoded.
                LogProviderFailed(provider.ProviderName, ex);
            }
        }

        return null;
    }

    private List<IGeocodingProvider> OrderProviders(
        IEnumerable<IGeocodingProvider> providers,
        IList<string> order)
    {
        List<IGeocodingProvider> registered = [.. providers];
        if (order.Count == 0)
        {
            return registered;
        }

        Dictionary<string, IGeocodingProvider> byName = new(StringComparer.OrdinalIgnoreCase);
        foreach (IGeocodingProvider provider in registered)
        {
            byName.TryAdd(provider.ProviderName, provider);
        }

        List<IGeocodingProvider> ordered = [];
        foreach (string name in order)
        {
            if (byName.TryGetValue(name, out IGeocodingProvider? provider))
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

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Geocoding provider '{Provider}' failed; falling back to the next provider.")]
    private partial void LogProviderFailed(string provider, Exception exception);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Debug,
        Message = "Geocoding resolution skipped ({Reason}); returning null.")]
    private partial void LogResolutionSkipped(string reason);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Configured geocoding provider '{Provider}' is not registered and will be skipped.")]
    private partial void LogUnknownProvider(string provider);
}
