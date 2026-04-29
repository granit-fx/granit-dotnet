using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Parties.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Parties.Metrics;

/// <summary>
/// Total number of parties across all lifecycle states — overall counterparty
/// universe size. Useful paired with <c>ActivePartyCount</c>: the gap reveals
/// suspended + archived volume.
/// </summary>
public sealed class PartyCountMetricDefinition : MetricDefinition<Party, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Parties.PartyCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<Party, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<Party, DateTimeOffset>>? PeriodSelector
        => p => p.CreatedAt;
}
