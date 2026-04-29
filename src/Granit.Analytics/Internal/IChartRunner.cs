using Granit.QueryEngine.Filtering;

namespace Granit.Analytics.Internal;

/// <summary>
/// Non-generic façade over a typed <c>QueryDefinition&lt;TEntity&gt;</c> that
/// runs a group-by aggregation against the entity's queryable. One runner is
/// registered per query definition by
/// <c>AddGranitAnalyticsWidgetRenderers</c> at startup; the dashboard render
/// path resolves it by query name.
/// </summary>
/// <remarks>
/// <para>
/// Wires every <see cref="AggregateFunction"/> member (B3-5 + B3-5bis):
/// <see cref="AggregateFunction.Count"/> uses
/// <c>IQueryEngine&lt;TEntity&gt;.ExecuteGroupedAsync</c> for SQL-level
/// grouping; the numeric aggregations
/// (<see cref="AggregateFunction.Sum"/> / <see cref="AggregateFunction.Avg"/>
/// / <see cref="AggregateFunction.Min"/> / <see cref="AggregateFunction.Max"/>)
/// stream the filtered entity set through <c>ExecuteStreamAsync</c> and
/// compute the per-group aggregate in memory. The stream is capped by the
/// QueryDefinition's <c>MaxStreamSize</c> — chart tiles are bounded data
/// products and the in-memory pass keeps the implementation
/// reflection-only at the property-access level (no expression-tree
/// surgery to push the aggregation to SQL).
/// </para>
/// <para>
/// Empty-set semantics shared with the metric path (locked by tests #1374):
/// </para>
/// <list type="bullet">
///   <item><c>Count</c> empty → <c>0</c>. <c>Sum</c> empty → <c>0</c> (sum-of-empty identity).</item>
///   <item><c>Avg</c> / <c>Min</c> / <c>Max</c> empty group → <see langword="null"/> on the bucket value (the bucket is still reported; the frontend renders "—" for that data point).</item>
/// </list>
/// <para>
/// Configuration errors throw — <c>IDashboardRenderer</c>'s per-widget error
/// isolation surfaces those as <c>Error</c> envelopes per ADR-039 §3.c.
/// Unknown field, unsupported field type and empty group-by all fall in
/// this category.
/// </para>
/// </remarks>
internal interface IChartRunner
{
    /// <summary>The query definition's unique name.</summary>
    string Name { get; }

    /// <summary>
    /// Runs <paramref name="aggregation"/> over the entity's queryable, grouped
    /// by <paramref name="groupBy"/>. Returns one bucket per group, ordered by
    /// the underlying query result. Buckets carry <see langword="null"/>
    /// values for empty-group <c>Avg</c> / <c>Min</c> / <c>Max</c>.
    /// </summary>
    /// <param name="groupBy">Group-by field name (case-insensitive).</param>
    /// <param name="aggregation">Aggregation function applied per group.</param>
    /// <param name="field">Field aggregated; required for non-Count aggregations, ignored for <see cref="AggregateFunction.Count"/>.</param>
    /// <param name="dashboardFilters">Dashboard-level filter spec layered on top of the QueryDefinition's filter pipeline — see <see cref="DashboardFilterTranslator"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">Group-by is null/empty, or non-Count aggregation is missing a field, or the field is not a property of the entity.</exception>
    /// <exception cref="NotSupportedException">Field's primitive type is not one of int / long / decimal / double.</exception>
    Task<ChartRunnerResult> ExecuteAsync(
        string groupBy,
        AggregateFunction aggregation,
        string? field,
        IReadOnlyDictionary<string, string>? dashboardFilters,
        CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of an <see cref="IChartRunner.ExecuteAsync"/> call. One bucket per
/// group; <see cref="ChartRunnerBucket.Label"/> renders directly,
/// <see cref="ChartRunnerBucket.Value"/> is the projected aggregate value.
/// </summary>
/// <param name="Buckets">Group-aggregate results in the order surfaced by the underlying query.</param>
/// <param name="CurrencyCode">ISO 4217 alpha-3 code from the value field's column descriptor (declared via <c>ColumnBuilder.Currency</c>); <see langword="null"/> for non-monetary aggregations and for <c>Count</c>. All buckets in the series share the same currency since they aggregate the same value field.</param>
internal sealed record ChartRunnerResult(
    IReadOnlyList<ChartRunnerBucket> Buckets,
    string? CurrencyCode = null);

/// <summary>One group's contribution to the chart series.</summary>
/// <param name="Label">String-friendly group key. Null group keys surface as <c>"(null)"</c>.</param>
/// <param name="Value">Aggregate value. <see langword="null"/> when the aggregation is <c>Avg</c>/<c>Min</c>/<c>Max</c> over a group with no usable values (frontend renders "—").</param>
internal sealed record ChartRunnerBucket(string Label, decimal? Value);
