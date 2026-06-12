using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.IpGeolocation.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the IP geolocation module.
/// Meter: <c>Granit.IpGeolocation</c>.
/// </summary>
/// <remarks>
/// IP geolocation is a global, stateless infrastructure concern, so the mandatory <c>tenant_id</c> tag is
/// always the coalesced default <c>"global"</c> — lookups are never tenant-scoped.
/// </remarks>
public sealed class IpGeolocationMetrics
{
    /// <summary>The meter name.</summary>
    public const string MeterName = "Granit.IpGeolocation";

    private const string TagTenantId = "tenant_id";
    private const string GlobalTenant = "global";

    private readonly Counter<long> _lookups;
    private readonly Counter<long> _cacheHits;
    private readonly Counter<long> _cacheMisses;
    private readonly Counter<long> _providerAttempts;
    private readonly Histogram<double> _lookupDuration;

    /// <summary>Initializes the metrics from the shared <see cref="IMeterFactory"/>.</summary>
    public IpGeolocationMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _lookups = meter.CreateCounter<long>(
            "granit.ip_geolocation.lookups",
            description: "Number of resolver lookups, tagged with the terminal result.");

        _cacheHits = meter.CreateCounter<long>(
            "granit.ip_geolocation.cache.hits",
            description: "Number of lookups served from the result cache (including negative cache).");

        _cacheMisses = meter.CreateCounter<long>(
            "granit.ip_geolocation.cache.misses",
            description: "Number of lookups that missed the cache and queried providers.");

        _providerAttempts = meter.CreateCounter<long>(
            "granit.ip_geolocation.provider.attempts",
            description: "Number of provider lookup attempts across the fallback chain.");

        _lookupDuration = meter.CreateHistogram<double>(
            "granit.ip_geolocation.lookup.duration",
            unit: "s",
            description: "End-to-end resolver lookup duration in seconds.");
    }

    /// <summary>Records a completed lookup. <paramref name="result"/> is one of <c>found</c>, <c>not_found</c>, <c>skipped</c>.</summary>
    public void RecordLookup(string result, string ipVersion) =>
        _lookups.Add(1, new TagList
        {
            { TagTenantId, GlobalTenant },
            { "result", result },
            { "ip_version", ipVersion },
        });

    /// <summary>Records a cache hit.</summary>
    public void RecordCacheHit() =>
        _cacheHits.Add(1, new TagList { { TagTenantId, GlobalTenant } });

    /// <summary>Records a cache miss.</summary>
    public void RecordCacheMiss() =>
        _cacheMisses.Add(1, new TagList { { TagTenantId, GlobalTenant } });

    /// <summary>Records a provider attempt. <paramref name="outcome"/> is one of <c>hit</c>, <c>miss</c>, <c>error</c>.</summary>
    public void RecordProviderAttempt(string provider, string outcome) =>
        _providerAttempts.Add(1, new TagList
        {
            { TagTenantId, GlobalTenant },
            { "provider", provider },
            { "outcome", outcome },
        });

    /// <summary>Records the end-to-end duration of a resolver lookup.</summary>
    public void RecordLookupDuration(string result, TimeSpan duration) =>
        _lookupDuration.Record(duration.TotalSeconds, new TagList
        {
            { TagTenantId, GlobalTenant },
            { "result", result },
        });
}
