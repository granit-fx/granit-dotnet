using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Identity.Federated.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Identity.Federated.Metrics;

/// <summary>
/// Total number of cached federated user entries — the universe size of the
/// federated identity cache. Useful paired with
/// <c>EnabledUserCacheEntryCount</c>: the gap reveals disabled / suspended
/// volume.
/// </summary>
public sealed class UserCacheEntryCountMetricDefinition : MetricDefinition<UserCacheEntry, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Identity.Federated.UserCacheEntryCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<UserCacheEntry, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<UserCacheEntry, DateTimeOffset>>? PeriodSelector
        => u => u.LastSyncedAt;
}
