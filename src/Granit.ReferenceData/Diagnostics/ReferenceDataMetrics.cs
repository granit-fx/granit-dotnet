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
    private const string TagEntityType = "entity_type";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _entriesQueried;
    private readonly Counter<long> _entriesCreated;
    private readonly Counter<long> _entriesUpdated;
    private readonly Counter<long> _entriesDeactivated;
    private readonly Counter<long> _cacheMisses;
    private readonly Counter<long> _seedsExecuted;

    public ReferenceDataMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _entriesQueried = meter.CreateCounter<long>(
            "granit.reference_data.entry.queried",
            description: "Number of reference data entry queries.");

        _entriesCreated = meter.CreateCounter<long>(
            "granit.reference_data.entry.created",
            description: "Number of reference data entries created.");

        _entriesUpdated = meter.CreateCounter<long>(
            "granit.reference_data.entry.updated",
            description: "Number of reference data entries updated.");

        _entriesDeactivated = meter.CreateCounter<long>(
            "granit.reference_data.entry.deactivated",
            description: "Number of reference data entries deactivated.");

        _cacheMisses = meter.CreateCounter<long>(
            "granit.reference_data.cache.miss",
            description: "Number of reference data cache misses requiring DB fetch.");

        _seedsExecuted = meter.CreateCounter<long>(
            "granit.reference_data.seed.executed",
            description: "Number of reference data seed operations executed.");
    }

    public void RecordEntryQueried(string? tenantId, string entityType) =>
        _entriesQueried.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, entityType },
        });

    public void RecordEntryCreated(string? tenantId, string entityType) =>
        _entriesCreated.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, entityType },
        });

    public void RecordEntryUpdated(string? tenantId, string entityType) =>
        _entriesUpdated.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, entityType },
        });

    public void RecordEntryDeactivated(string? tenantId, string entityType) =>
        _entriesDeactivated.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, entityType },
        });

    public void RecordCacheMiss(string? tenantId, string entityType) =>
        _cacheMisses.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, entityType },
        });

    public void RecordSeedExecuted(string? tenantId, string entityType, string seederName) =>
        _seedsExecuted.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, entityType },
            { "seeder_name", seederName },
        });
}
