using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Catalog.Domain;
using Granit.QueryEngine.Filtering;
using Granit.Workflow.Domain;

namespace Granit.Catalog.Metrics;

/// <summary>
/// Number of products currently in <see cref="WorkflowLifecycleStatus.Published"/>
/// — the catalog surface available to subscribers and order flows. Drafts and
/// archived products are excluded.
/// </summary>
public sealed class ActiveProductCountMetricDefinition : MetricDefinition<Product, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Catalog.ActiveProductCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<Product, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<Product, bool>>? BaseFilter
        => p => p.LifecycleStatus == WorkflowLifecycleStatus.Published;

    /// <inheritdoc />
    public override Expression<Func<Product, DateTimeOffset>>? PeriodSelector
        => p => p.CreatedAt;
}
