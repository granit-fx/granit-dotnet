using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Documents.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Documents.Metrics;

/// <summary>
/// Total bytes currently consumed by the tenant's documents — sum of
/// <see cref="TenantStorageQuota.UsageBytes"/>. Within a single tenant the multi-tenancy
/// query filter narrows to one row, so the <see cref="AggregateFunction.Sum"/> resolves
/// to that row's usage value; cross-tenant aggregation only applies to platform-admin
/// hosts that intentionally drop the filter.
/// </summary>
/// <remarks>
/// <para>
/// Reading from <c>TenantStorageQuota</c> rather than re-aggregating
/// <c>DocumentVersion.SizeBytes</c> on every dashboard tick is intentional — the
/// quota row is maintained atomically by <c>ITenantQuotaService</c> on every
/// upload / permanent-delete, and reconciled to the source of truth by the F9.3
/// <c>QuotaRecomputeJob</c>.
/// </para>
/// <para>
/// <c>IsHigherBetter</c> is <c>false</c>: lower storage consumption is favourable.
/// </para>
/// </remarks>
public sealed class TotalStorageUsedMetricDefinition : MetricDefinition<TenantStorageQuota, long>
{
    /// <inheritdoc/>
    public override string Name => "Granit.Documents.TotalStorageUsedMetric";

    /// <inheritdoc/>
    public override MetricValueKind ValueKind => MetricValueKind.Number;

    /// <inheritdoc/>
    public override AggregateFunction Aggregation => AggregateFunction.Sum;

    /// <inheritdoc/>
    public override Expression<Func<TenantStorageQuota, long?>>? Selector
        => q => q.UsageBytes;

    /// <inheritdoc/>
    public override bool IsHigherBetter => false;
}
