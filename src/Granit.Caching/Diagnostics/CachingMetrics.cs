using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Caching.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the caching module.
/// Meter: <c>Granit.Caching</c>.
/// </summary>
public sealed class CachingMetrics
{
    public const string MeterName = "Granit.Caching";

    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;
    private readonly Counter<long> _failSafeActivations;
    private readonly Counter<long> _factoryTimeouts;

    public CachingMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _hits = meter.CreateCounter<long>(
            "granit.caching.entry.hit",
            description: "Number of cache hits.");

        _misses = meter.CreateCounter<long>(
            "granit.caching.entry.miss",
            description: "Number of cache misses.");

        _failSafeActivations = meter.CreateCounter<long>(
            "granit.caching.fail_safe.activated",
            description: "Number of fail-safe activations (stale entry served).");

        _factoryTimeouts = meter.CreateCounter<long>(
            "granit.caching.factory.timeout",
            description: "Number of factory execution timeouts (soft or hard).");
    }

    public void RecordHit(string? tenantId) =>
        _hits.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
        });

    public void RecordMiss(string? tenantId) =>
        _misses.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
        });

    public void RecordFailSafeActivated(string? tenantId) =>
        _failSafeActivations.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
        });

    public void RecordFactoryTimeout(string? tenantId, string timeoutType) =>
        _factoryTimeouts.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "timeout_type", timeoutType },
        });
}
