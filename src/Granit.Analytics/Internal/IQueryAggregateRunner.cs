using Granit.QueryEngine.Filtering;

namespace Granit.Analytics.Internal;

/// <summary>
/// Non-generic façade over a typed <c>QueryDefinition&lt;TEntity&gt;</c> that runs
/// an <see cref="AggregateFunction"/> against the entity's
/// <c>IQueryableSource&lt;TEntity&gt;</c>. One runner is registered per query
/// definition by <c>AddGranitAnalyticsWidgetRenderers</c> at startup, so the
/// dashboard render path resolves it by query name without reflection.
/// </summary>
/// <remarks>
/// <para>
/// All five <see cref="AggregateFunction"/> members are wired (B3-2bis +
/// B3-2ter): <see cref="AggregateFunction.Count"/> takes no field;
/// <see cref="AggregateFunction.Sum"/>, <see cref="AggregateFunction.Avg"/>,
/// <see cref="AggregateFunction.Min"/>, <see cref="AggregateFunction.Max"/>
/// resolve the field name reflectively against the closed entity type
/// and dispatch to EF Core's typed aggregator per primitive type
/// (<c>int</c> / <c>long</c> / <c>decimal</c> / <c>double</c>).
/// </para>
/// <para>
/// Empty-set semantics shared with the metric path (locked by tests #1374):
/// </para>
/// <list type="bullet">
///   <item><c>Count</c> empty → <c>0</c>.</item>
///   <item><c>Sum</c> empty → <c>0</c> (sum-of-empty identity).</item>
///   <item><c>Avg</c> / <c>Min</c> / <c>Max</c> empty → <see langword="null"/> (semantic "no data"; division by zero / empty-set extremum is undefined).</item>
/// </list>
/// <para>
/// Configuration errors (unknown field, unsupported field type) throw —
/// <c>IDashboardRenderer</c>'s per-widget error isolation surfaces those as
/// <c>Error</c> envelopes per ADR-039 §3.c. The dashboard render keeps
/// returning 200; the misconfigured widget is the only one affected.
/// </para>
/// </remarks>
internal interface IQueryAggregateRunner
{
    /// <summary>The query definition's unique name.</summary>
    string Name { get; }

    /// <summary>
    /// Executes <paramref name="aggregation"/> over the entity's queryable.
    /// Returns the projected <see cref="decimal"/> value, or <see langword="null"/>
    /// for empty-set Avg / Min / Max. <c>Count</c> and <c>Sum</c> never return
    /// null (they coerce to <c>0</c>).
    /// </summary>
    /// <param name="aggregation">Aggregation function to run.</param>
    /// <param name="field">Field name for non-Count aggregations; required (not validated when <paramref name="aggregation"/> is <see cref="AggregateFunction.Count"/>). Resolved against the closed entity type reflectively (case-insensitive); unknown field throws.</param>
    /// <param name="dashboardFilters">Dashboard-level filter spec layered on top of the underlying QueryDefinition's filter pipeline — see <see cref="DashboardFilterTranslator"/>. <see langword="null"/> when called outside a dashboard render.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">Field is null/empty for a non-Count aggregation, or field is not a property of the entity.</exception>
    /// <exception cref="NotSupportedException">Field's primitive type is not one of int / long / decimal / double.</exception>
    Task<QueryAggregateRunnerResult> ExecuteAsync(
        AggregateFunction aggregation,
        string? field,
        IReadOnlyDictionary<string, string>? dashboardFilters,
        CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of an <see cref="IQueryAggregateRunner.ExecuteAsync"/> call.
/// Bundles the projected value with the column's ISO 4217 currency code
/// (when declared on the QueryDefinition's column descriptor), so the
/// evaluator can promote the snapshot to <c>MetricValueKind.Currency</c>
/// without re-traversing the query metadata.
/// </summary>
/// <param name="Value">Projected aggregate value, coerced to <see cref="decimal"/>; <see langword="null"/> for empty Avg/Min/Max.</param>
/// <param name="CurrencyCode">ISO 4217 alpha-3 code from the underlying column's <c>Currency(...)</c> declaration; <see langword="null"/> for non-monetary aggregations and for <c>Count</c>.</param>
internal sealed record QueryAggregateRunnerResult(decimal? Value, string? CurrencyCode);
