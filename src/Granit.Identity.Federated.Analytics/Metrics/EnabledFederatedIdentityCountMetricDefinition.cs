using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Identity.Federated.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Identity.Federated.Analytics.Metrics;

/// <summary>
/// Number of cached federated users currently in <see cref="FederatedIdentity.Enabled"/>
/// state — the addressable identity pool. Disabled entries (suspended,
/// deactivated upstream) are excluded.
/// </summary>
public sealed class EnabledFederatedIdentityCountMetricDefinition : MetricDefinition<FederatedIdentity, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Identity.Federated.EnabledFederatedIdentityCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<FederatedIdentity, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<FederatedIdentity, bool>>? BaseFilter
        => u => u.Enabled;

    /// <inheritdoc />
    public override Expression<Func<FederatedIdentity, DateTimeOffset>>? PeriodSelector
        => u => u.LastSyncedAt;
}
