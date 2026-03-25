using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Authorization.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the authorization module.
/// Meter: <c>Granit.Authorization</c>.
/// </summary>
public sealed class AuthorizationMetrics
{
    public const string MeterName = "Granit.Authorization";

    private readonly Counter<long> _checksGranted;
    private readonly Counter<long> _checksDenied;
    private readonly Counter<long> _cacheHits;
    private readonly Counter<long> _cacheMisses;

    public AuthorizationMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _checksGranted = meter.CreateCounter<long>(
            "granit.authorization.check.granted",
            description: "Number of permission checks that were granted.");

        _checksDenied = meter.CreateCounter<long>(
            "granit.authorization.check.denied",
            description: "Number of permission checks that were denied.");

        _cacheHits = meter.CreateCounter<long>(
            "granit.authorization.cache.hit",
            description: "Number of permission grant cache hits.");

        _cacheMisses = meter.CreateCounter<long>(
            "granit.authorization.cache.miss",
            description: "Number of permission grant cache misses.");
    }

    public void RecordCheckGranted(string? tenantId, string permissionName) =>
        _checksGranted.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "permission_name", permissionName },
        });

    public void RecordCheckDenied(string? tenantId, string permissionName) =>
        _checksDenied.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "permission_name", permissionName },
        });

    public void RecordCacheHit(string? tenantId) =>
        _cacheHits.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
        });

    public void RecordCacheMiss(string? tenantId) =>
        _cacheMisses.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
        });
}
