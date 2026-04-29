using Granit.QueryEngine.Filtering;

namespace Granit.Analytics.Endpoints.Internal;

/// <summary>
/// Non-generic façade over a typed <c>QueryDefinition&lt;TEntity&gt;</c> that
/// runs a group-by aggregation against the entity's queryable. One runner is
/// registered per query definition by
/// <c>AddGranitAnalyticsWidgetRenderers</c> at startup; the dashboard render
/// path resolves it by query name.
/// </summary>
/// <remarks>
/// <para>
/// B3-5 ships <see cref="AggregateFunction.Count"/> only — the most common
/// chart shape ("invoices per status", "tickets per priority"). Reuses
/// <c>IQueryEngine&lt;TEntity&gt;.ExecuteGroupedAsync</c> for the heavy
/// lifting so chart tiles inherit the same group-by semantics, multi-tenancy
/// filter and MaxGroupCount cap as the grid endpoint's grouping pivot.
/// </para>
/// <para>
/// <see cref="AggregateFunction.Sum"/> / <see cref="AggregateFunction.Avg"/> /
/// <see cref="AggregateFunction.Min"/> / <see cref="AggregateFunction.Max"/>
/// need a typed group-by expression plus per-primitive-type dispatch
/// (mirrors <c>QueryAggregateRunner</c> B3-2ter, with an extra GroupBy axis);
/// they ship in a follow-up slice. Until then the runner returns
/// <see langword="null"/> for those operations and the caller surfaces a
/// dedicated <c>Widget:Unavailable.*</c> reason.
/// </para>
/// </remarks>
internal interface IChartRunner
{
    /// <summary>The query definition's unique name.</summary>
    string Name { get; }

    /// <summary>
    /// Runs <paramref name="aggregation"/> over the entity's queryable, grouped
    /// by <paramref name="groupBy"/>. Returns one bucket per group, ordered as
    /// the underlying <c>ExecuteGroupedAsync</c> orders them. Returns
    /// <see langword="null"/> when the aggregation is not yet implemented
    /// (Sum / Avg / Min / Max in B3-5).
    /// </summary>
    /// <param name="groupBy">Group-by field name (case-insensitive). Resolved against the QueryDefinition's column descriptors; unknown field is rejected upstream by the QueryEngine.</param>
    /// <param name="aggregation">Aggregation function applied per group.</param>
    /// <param name="field">Field aggregated; required for non-Count aggregations, ignored for <see cref="AggregateFunction.Count"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ChartRunnerResult?> ExecuteAsync(
        string groupBy,
        AggregateFunction aggregation,
        string? field,
        CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of an <see cref="IChartRunner.ExecuteAsync"/> call. One bucket per
/// group; <see cref="ChartRunnerBucket.Label"/> renders directly, <see cref="ChartRunnerBucket.Value"/>
/// is the projected aggregate value coerced to <see cref="decimal"/>.
/// </summary>
/// <param name="Buckets">Group-aggregate results in the order surfaced by <c>IQueryEngine.ExecuteGroupedAsync</c>.</param>
internal sealed record ChartRunnerResult(IReadOnlyList<ChartRunnerBucket> Buckets);

/// <summary>One group's contribution to the chart series.</summary>
/// <param name="Label">String-friendly group key (e.g. <c>"Open"</c>, <c>"Paid"</c>, <c>"2026-04"</c>). The QueryEngine builds it from the raw key; null group keys surface as <c>"(null)"</c>.</param>
/// <param name="Value">Aggregate value for the group. Always a count (B3-5 Count-only); future Sum/Avg/Min/Max land here once the typed dispatch slice ships.</param>
internal sealed record ChartRunnerBucket(string Label, decimal Value);
