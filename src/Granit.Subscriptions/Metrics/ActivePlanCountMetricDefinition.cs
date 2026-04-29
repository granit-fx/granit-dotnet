using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.QueryEngine.Filtering;
using Granit.Subscriptions.Domain;
using Granit.Workflow.Domain;

namespace Granit.Subscriptions.Metrics;

/// <summary>
/// Number of plans currently in <see cref="WorkflowLifecycleStatus.Published"/> —
/// catalog breadth available to new and existing subscribers. Drafts and archived
/// plans are excluded.
/// </summary>
public sealed class ActivePlanCountMetricDefinition : MetricDefinition<Plan, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Subscriptions.ActivePlanCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<Plan, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<Plan, bool>>? BaseFilter
        => p => p.LifecycleStatus == WorkflowLifecycleStatus.Published;

    /// <inheritdoc />
    public override Expression<Func<Plan, DateTimeOffset>>? PeriodSelector
        => p => p.CreatedAt;
}
