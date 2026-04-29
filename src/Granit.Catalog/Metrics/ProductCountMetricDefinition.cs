using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Catalog.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Catalog.Metrics;

/// <summary>
/// Total number of products in the catalog across all lifecycle states. Useful for
/// catalog-growth tracking when paired with <c>ActiveProductCount</c>: the gap
/// between the two reveals draft + archived volume.
/// </summary>
public sealed class ProductCountMetricDefinition : MetricDefinition<Product, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Catalog.ProductCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<Product, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<Product, DateTimeOffset>>? PeriodSelector
        => p => p.CreatedAt;
}
