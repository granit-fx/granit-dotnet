using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Identity.Federated.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Identity.Federated.Metrics;

/// <summary>
/// Total number of cached federated user entries — the universe size of the
/// federated identity cache. Useful paired with
/// <c>EnabledFederatedIdentityCount</c>: the gap reveals disabled / suspended
/// volume.
/// </summary>
public sealed class FederatedIdentityCountMetricDefinition : MetricDefinition<FederatedIdentity, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Identity.Federated.FederatedIdentityCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<FederatedIdentity, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<FederatedIdentity, DateTimeOffset>>? PeriodSelector
        => u => u.LastSyncedAt;
}
