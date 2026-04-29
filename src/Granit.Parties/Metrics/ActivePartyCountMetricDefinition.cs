using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Parties.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Parties.Metrics;

/// <summary>
/// Number of parties currently in <see cref="PartyStatus.Active"/> — the live
/// counterparties in the system. Suspended and Archived parties are excluded.
/// </summary>
public sealed class ActivePartyCountMetricDefinition : MetricDefinition<Party, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Parties.ActivePartyCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<Party, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<Party, bool>>? BaseFilter
        => p => p.Status == PartyStatus.Active;

    /// <inheritdoc />
    public override Expression<Func<Party, DateTimeOffset>>? PeriodSelector
        => p => p.CreatedAt;
}
