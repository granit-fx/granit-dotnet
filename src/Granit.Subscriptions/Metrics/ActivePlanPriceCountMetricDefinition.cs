using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.QueryEngine.Filtering;
using Granit.Subscriptions.Domain;

namespace Granit.Subscriptions.Metrics;

/// <summary>
/// Number of plan prices currently in effect — those that have NOT been replaced
/// by a newer pricing version (<see cref="PlanPrice.ReplacedAt"/> is <c>null</c>).
/// Reflects the active pricing surface offered to subscribers.
/// </summary>
public sealed class ActivePlanPriceCountMetricDefinition : MetricDefinition<PlanPrice, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Subscriptions.ActivePlanPriceCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<PlanPrice, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<PlanPrice, bool>>? BaseFilter
        => p => p.ReplacedAt == null;

    /// <inheritdoc />
    public override Expression<Func<PlanPrice, DateTimeOffset>>? PeriodSelector
        => p => p.EffectiveFrom;
}
