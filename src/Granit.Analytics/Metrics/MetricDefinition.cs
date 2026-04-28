using System.Linq.Expressions;
using Granit.QueryEngine.Filtering;

namespace Granit.Analytics.Metrics;

/// <summary>
/// Base class for declaring how an entity is aggregated into a single value
/// (count, sum, average, min, max). Pairs with <c>QueryDefinition</c> (lists rows)
/// and <c>ExportDefinition</c> (extracts rows).
/// </summary>
/// <typeparam name="TEntity">The target entity type.</typeparam>
/// <typeparam name="TValue">The result type of the aggregation.</typeparam>
/// <remarks>
/// <para>
/// Each metric definition is registered as a singleton via
/// <c>services.AddMetricDefinition&lt;TEntity, TDefinition&gt;()</c>. Definitions are
/// pure declarations — they describe the aggregation but do not execute it. The
/// <c>MetricExecutor&lt;TEntity, TValue&gt;</c> in <c>Granit.Analytics.EntityFrameworkCore</c>
/// runs them through the same filter pipeline used by the grid endpoints, so a KPI
/// "12 unpaid invoices" exactly matches the grid's row count for the same filter.
/// </para>
/// <para>
/// Empty-set semantics (locked by tests in story #1374): <c>Count</c> and <c>Sum</c> over
/// an empty set return <c>0</c> (mathematically defined). <c>Avg</c>, <c>Min</c>, <c>Max</c>
/// return <c>null</c> ("no data") — never zero, never an exception. The
/// <see cref="Selector"/> is intentionally typed nullable
/// (<c>Expression&lt;Func&lt;TEntity, TValue?&gt;&gt;</c>) to avoid the EF Core
/// <see cref="InvalidOperationException"/> thrown when SQL <c>SUM</c>/<c>AVG</c> returns
/// <c>NULL</c> on an empty set.
/// </para>
/// <para>
/// Example:
/// <code>
/// public sealed class UnpaidInvoiceCountMetric : MetricDefinition&lt;Invoice, int&gt;
/// {
///     public override string Name =&gt; "Granit.Invoicing.UnpaidInvoiceCount";
///     public override MetricValueKind ValueKind =&gt; MetricValueKind.Count;
///     public override AggregateFunction Aggregation =&gt; AggregateFunction.Count;
///     public override Expression&lt;Func&lt;Invoice, int?&gt;&gt;? Selector =&gt; null;
///     public override bool IsHigherBetter =&gt; false;
///     public override RefreshHint RefreshHint =&gt; RefreshHint.Dynamic;
/// }
/// </code>
/// </para>
/// </remarks>
public abstract class MetricDefinition<TEntity, TValue> : IMetricDefinitionDescriptor
    where TEntity : class
    where TValue : struct
{
    /// <summary>Unique name identifying this metric (e.g. <c>"Granit.Invoicing.UnpaidInvoiceCount"</c>).</summary>
    public abstract string Name { get; }

    /// <summary>The semantic kind of the value (count, currency, percentage, ...).</summary>
    public abstract MetricValueKind ValueKind { get; }

    /// <summary>
    /// The aggregation function (<c>Count</c>, <c>Sum</c>, <c>Avg</c>, <c>Min</c>, <c>Max</c>).
    /// Reused from <see cref="AggregateFunction"/> to avoid duplicating the enum.
    /// </summary>
    public abstract AggregateFunction Aggregation { get; }

    /// <summary>
    /// The projection expression selecting the value to aggregate. <c>null</c> for
    /// <see cref="AggregateFunction.Count"/> (no selector needed).
    /// </summary>
    /// <remarks>
    /// Type intentionally <c>TValue?</c> (nullable) so EF Core's <c>SumAsync</c> /
    /// <c>AverageAsync</c> return <c>null</c> on an empty set instead of throwing.
    /// </remarks>
    public abstract Expression<Func<TEntity, TValue?>>? Selector { get; }

    /// <summary>
    /// ISO 4217 currency code when <see cref="ValueKind"/> is <see cref="MetricValueKind.Currency"/>.
    /// Default: <c>null</c>; concrete metrics override per tenant via options.
    /// </summary>
    public virtual string? CurrencyCode => null;

    /// <summary>
    /// Whether higher values are considered favorable. Drives the delta arrow color
    /// in the frontend (green when delta direction × <c>IsHigherBetter</c> is favorable, red otherwise).
    /// </summary>
    public virtual bool IsHigherBetter => true;

    /// <summary>
    /// How fresh the metric is expected to be. Pull-based renderers (v1) honor
    /// <see cref="RefreshHint.Static"/> and <see cref="RefreshHint.Dynamic"/> to pick a
    /// FusionCache TTL. <see cref="RefreshHint.Realtime"/> is reserved — it requires the
    /// push transport scheduled in the <c>granit-iot</c> repo.
    /// </summary>
    public virtual RefreshHint RefreshHint => RefreshHint.Dynamic;

    /// <inheritdoc />
    Type IMetricDefinitionDescriptor.EntityType => typeof(TEntity);

    /// <inheritdoc />
    Type IMetricDefinitionDescriptor.ValueType => typeof(TValue);
}
