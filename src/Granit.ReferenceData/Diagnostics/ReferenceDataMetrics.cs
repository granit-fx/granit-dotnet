using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.ReferenceData.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the reference data module.
/// Meter: <c>Granit.ReferenceData</c>.
/// </summary>
public sealed class ReferenceDataMetrics
{
    public const string MeterName = "Granit.ReferenceData";

    private const string TagTenantId = "tenant_id";
    private const string TagTypeName = "type_name";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _entriesCreated;
    private readonly Counter<long> _entriesUpdated;
    private readonly Counter<long> _entriesDeactivated;
    private readonly Counter<long> _cacheHits;
    private readonly Counter<long> _cacheMisses;

    public ReferenceDataMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _entriesCreated = meter.CreateCounter<long>(
            "granit.reference_data.entry.created",
            description: "Number of reference data entries created.");

        _entriesUpdated = meter.CreateCounter<long>(
            "granit.reference_data.entry.updated",
            description: "Number of reference data entries updated.");

        _entriesDeactivated = meter.CreateCounter<long>(
            "granit.reference_data.entry.deactivated",
            description: "Number of reference data entries deactivated.");

        _cacheHits = meter.CreateCounter<long>(
            "granit.reference_data.cache.hit",
            description: "Number of cache hits for reference data lookups.");

        _cacheMisses = meter.CreateCounter<long>(
            "granit.reference_data.cache.miss",
            description: "Number of cache misses for reference data lookups.");
    }

    public void RecordEntryCreated(string? tenantId, string typeName) =>
        _entriesCreated.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagTypeName, typeName },
        });

    public void RecordEntryUpdated(string? tenantId, string typeName) =>
        _entriesUpdated.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagTypeName, typeName },
        });

    public void RecordEntryDeactivated(string? tenantId, string typeName) =>
        _entriesDeactivated.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagTypeName, typeName },
        });

    public void RecordCacheHit(string? tenantId, string typeName) =>
        _cacheHits.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagTypeName, typeName },
        });

    public void RecordCacheMiss(string? tenantId, string typeName) =>
        _cacheMisses.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagTypeName, typeName },
        });
}
