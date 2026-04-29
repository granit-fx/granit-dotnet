using Granit.QueryEngine.Filtering;

namespace Granit.Analytics.Endpoints.Internal;

/// <summary>
/// Non-generic façade over a typed <c>QueryDefinition&lt;TEntity&gt;</c> that runs
/// an <see cref="AggregateFunction"/> against the entity's
/// <c>IQueryableSource&lt;TEntity&gt;</c>. One runner is registered per query
/// definition by <c>AddGranitAnalyticsWidgetRenderers</c> at startup, so the
/// dashboard render path resolves it by query name without reflection.
/// </summary>
/// <remarks>
/// <para>
/// B3-2bis ships <see cref="AggregateFunction.Count"/> only — the most common
/// KPI shape ("how many open invoices"). <see cref="AggregateFunction.Sum"/>,
/// <see cref="AggregateFunction.Avg"/>, <see cref="AggregateFunction.Min"/>,
/// <see cref="AggregateFunction.Max"/> need a typed selector built from the
/// runtime field name plus per-primitive-type dispatch (mirrors
/// <c>MetricExecutor&lt;TEntity, TValue&gt;</c>); they ship in a follow-up
/// slice. Until then, <see cref="ExecuteAsync"/> returns <see langword="null"/>
/// for those operations and the caller surfaces a dedicated
/// <c>Widget:Unavailable.*</c> reason.
/// </para>
/// <para>
/// Empty-set semantics shared with the metric path (locked by tests #1374):
/// <c>Count</c> over empty set returns <c>0</c>, never <see langword="null"/>.
/// </para>
/// </remarks>
internal interface IQueryAggregateRunner
{
    /// <summary>The query definition's unique name.</summary>
    string Name { get; }

    /// <summary>
    /// Executes <paramref name="aggregation"/> over the entity's queryable.
    /// Returns <see langword="null"/> when the aggregation is not yet implemented
    /// (Sum / Avg / Min / Max in B3-2bis). The caller maps a null result onto
    /// an Unavailable envelope.
    /// </summary>
    /// <param name="aggregation">Aggregation function to run.</param>
    /// <param name="field">Field name for non-Count aggregations; ignored when <paramref name="aggregation"/> is <see cref="AggregateFunction.Count"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<decimal?> ExecuteAsync(
        AggregateFunction aggregation,
        string? field,
        CancellationToken cancellationToken);
}
