using Granit.QueryEngine.Filtering;

namespace Granit.Analytics.Internal;

/// <summary>
/// Non-generic façade over a typed <c>QueryDefinition&lt;TEntity&gt;</c> that
/// runs a multi-axis pivot aggregation (rows × columns × value) against the
/// entity's queryable. One runner is registered per query definition by
/// <c>AddGranitAnalyticsWidgetRenderers</c> at startup; the dashboard render
/// path resolves it by query name.
/// </summary>
/// <remarks>
/// <para>
/// B3-6 streams the filtered entity set through
/// <c>IQueryEngine.ExecuteStreamAsync</c> (capped by <c>MaxStreamSize</c>) and
/// pivots in memory. Acceptable for dashboard tiles which are bounded data
/// products; future optimisation can push the multi-key GroupBy to SQL via
/// dynamic anonymous-type construction (mirrors the B3-5bis → B3-5ter
/// progression for charts).
/// </para>
/// <para>
/// Empty-set semantics shared with <c>MetricExecutor</c> (locked by tests #1374):
/// </para>
/// <list type="bullet">
///   <item><c>Count</c> / <c>Sum</c> over empty cells → <c>0</c>.</item>
///   <item><c>Avg</c> / <c>Min</c> / <c>Max</c> over empty cells → <see langword="null"/> on the cell value (frontend renders "—").</item>
/// </list>
/// </remarks>
internal interface IPivotRunner
{
    /// <summary>The query definition's unique name.</summary>
    string Name { get; }

    /// <summary>
    /// Runs the pivot aggregation. Returns one cell per (row-key tuple,
    /// column-key tuple) pair found in the data, in stream-arrival order.
    /// </summary>
    /// <param name="rowFields">Row-axis field names (case-insensitive). Must be a non-empty list of properties on the entity.</param>
    /// <param name="columnFields">Column-axis field names. Empty list means a single column dimension (one bucket per row tuple).</param>
    /// <param name="valueField">Field aggregated; required for non-Count aggregations, ignored for <see cref="AggregateFunction.Count"/>.</param>
    /// <param name="aggregation">Aggregation applied to <paramref name="valueField"/> per (row × column) cell.</param>
    /// <param name="dashboardFilters">Dashboard-level filter spec layered on top of the QueryDefinition's filter pipeline.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">A field name is unknown, or non-Count aggregation is missing a value field, or rowFields is empty.</exception>
    /// <exception cref="NotSupportedException">Value field's primitive type is not one of int / long / decimal / double.</exception>
    Task<PivotRunnerResult> ExecuteAsync(
        IReadOnlyList<string> rowFields,
        IReadOnlyList<string> columnFields,
        string? valueField,
        AggregateFunction aggregation,
        IReadOnlyDictionary<string, string>? dashboardFilters,
        CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of an <see cref="IPivotRunner.ExecuteAsync"/> call.
/// </summary>
/// <param name="Cells">Per-cell results in stream-arrival order. Each cell carries the row-key tuple and column-key tuple plus the aggregated value.</param>
/// <param name="CurrencyCode">ISO 4217 alpha-3 code from the value field's <c>ColumnBuilder.Currency(...)</c> declaration; <see langword="null"/> when the value field is not monetary or for <c>Count</c>. All cells share this currency since they aggregate the same value field.</param>
internal sealed record PivotRunnerResult(
    IReadOnlyList<PivotRunnerCell> Cells,
    string? CurrencyCode = null);

/// <summary>
/// One cell of the pivot matrix.
/// </summary>
/// <param name="RowKeys">Row-axis key values, ordered as in <c>rowFields</c>. Null property values surface as <c>"(null)"</c>.</param>
/// <param name="ColumnKeys">Column-axis key values, ordered as in <c>columnFields</c>. Empty when no column fields were requested.</param>
/// <param name="Value">Aggregate value. <see langword="null"/> when the aggregation is <c>Avg</c>/<c>Min</c>/<c>Max</c> over a cell with no usable values.</param>
internal sealed record PivotRunnerCell(
    IReadOnlyList<string> RowKeys,
    IReadOnlyList<string> ColumnKeys,
    decimal? Value);
