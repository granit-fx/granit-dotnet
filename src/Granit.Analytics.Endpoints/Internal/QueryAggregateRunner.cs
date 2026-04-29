using Granit.QueryEngine;
using Granit.QueryEngine.Filtering;
using Microsoft.EntityFrameworkCore;

namespace Granit.Analytics.Endpoints.Internal;

/// <summary>
/// Typed implementation of <see cref="IQueryAggregateRunner"/> — closes over
/// <typeparamref name="TEntity"/> so the dashboard render path dispatches by
/// query name without reflection at request time.
/// </summary>
/// <remarks>
/// Wraps the entity's <see cref="IQueryableSource{TEntity}"/> directly. The
/// dashboard render path does <b>not</b> push the dashboard's filter spec
/// through the QueryEngine pipeline yet — KPIs that need filtering should bind
/// to a <c>MetricDatasource</c> with a <c>BaseFilter</c> declared (which has
/// well-tested empty-set semantics). Filter-spec composition for query
/// aggregates is a follow-up slice.
/// </remarks>
internal sealed class QueryAggregateRunner<TEntity>(
    string name,
    IQueryableSource<TEntity> source) : IQueryAggregateRunner
    where TEntity : class
{
    private readonly IQueryableSource<TEntity> _source = source;

    public string Name { get; } = name;

    public async Task<decimal?> ExecuteAsync(
        AggregateFunction aggregation,
        string? field,
        CancellationToken cancellationToken)
    {
        if (aggregation != AggregateFunction.Count)
        {
            // Sum / Avg / Min / Max ship in a follow-up slice; the caller maps
            // null onto Widget:Unavailable.QueryAggregateOperationNotImplemented.
            return null;
        }

        IQueryable<TEntity> queryable = _source.GetQueryable();
        int count = await queryable.CountAsync(cancellationToken).ConfigureAwait(false);
        return count;
    }
}
