using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Persistence.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Metrics for Granit.Persistence.EntityFrameworkCore interceptors and data operations.
/// </summary>
/// <remarks>
/// Meter name: <c>Granit.Persistence.EntityFrameworkCore</c>. All metric names follow the
/// <c>granit.persistence.{entity}.{action}</c> convention.
/// </remarks>
public sealed class PersistenceMetrics
{
    public const string MeterName = "Granit.Persistence.EntityFrameworkCore";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _entitiesPurged;

    public PersistenceMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _entitiesPurged = meter.CreateCounter<long>(
            "granit.persistence.entity.purged",
            description: "Number of soft-deleted entities permanently purged.");
    }

    public void RecordEntitiesPurged(string? tenantId, int count) =>
        _entitiesPurged.Add(count, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
        });
}
