using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Parties.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Parties.Metrics;

/// <summary>
/// Number of parties that have been merged into another party in the period —
/// data-quality signal for the deduplication pipeline. Period selector is
/// <see cref="Party.MergedAt"/> so <c>?period=last_30d</c> answers "how many
/// duplicates we cleaned up this month?".
/// </summary>
public sealed class MergedPartyCountMetricDefinition : MetricDefinition<Party, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Parties.MergedPartyCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<Party, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<Party, bool>>? BaseFilter
        => p => p.MergedAt != null;

    /// <inheritdoc />
    public override Expression<Func<Party, DateTimeOffset>>? PeriodSelector
        => p => p.MergedAt!.Value;
}
