using Granit.QueryEngine;

namespace Granit.Analytics.Metrics;

/// <summary>
/// Executes a <see cref="MetricDefinition{TEntity, TValue}"/> against a base
/// <see cref="IQueryable{T}"/> through the <see cref="IQueryEngine{TEntity}"/> filter
/// pipeline. Returns the aggregated value with empty-set semantics applied.
/// </summary>
/// <typeparam name="TEntity">The target entity type.</typeparam>
/// <typeparam name="TValue">The aggregation result type.</typeparam>
public interface IMetricExecutor<TEntity, TValue>
    where TEntity : class
    where TValue : struct
{
    /// <summary>
    /// Runs the metric.
    /// </summary>
    /// <param name="metric">The metric definition.</param>
    /// <param name="source">The base queryable (already tenant- and soft-delete-filtered by the caller).</param>
    /// <param name="request">The query request (filters, presets, search).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <para>For <c>Count</c> and <c>Sum</c>: a value, never <c>null</c>; <c>0</c> on empty set.</para>
    /// <para>For <c>Avg</c>, <c>Min</c>, <c>Max</c>: <c>null</c> on empty set ("no data"); a value otherwise.</para>
    /// </returns>
    Task<TValue?> ExecuteAsync(
        MetricDefinition<TEntity, TValue> metric,
        IQueryable<TEntity> source,
        QueryRequest request,
        CancellationToken cancellationToken = default);
}
