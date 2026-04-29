using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Identity.Federated.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Identity.Federated.Metrics;

/// <summary>
/// Number of cached federated users currently in <see cref="UserCacheEntry.Enabled"/>
/// state — the addressable identity pool. Disabled entries (suspended,
/// deactivated upstream) are excluded.
/// </summary>
public sealed class EnabledUserCacheEntryCountMetricDefinition : MetricDefinition<UserCacheEntry, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Identity.Federated.EnabledUserCacheEntryCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<UserCacheEntry, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<UserCacheEntry, bool>>? BaseFilter
        => u => u.Enabled;

    /// <inheritdoc />
    public override Expression<Func<UserCacheEntry, DateTimeOffset>>? PeriodSelector
        => u => u.LastSyncedAt;
}
