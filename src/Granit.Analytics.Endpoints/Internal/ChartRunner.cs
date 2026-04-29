using Granit.QueryEngine;
using Granit.QueryEngine.Filtering;

namespace Granit.Analytics.Endpoints.Internal;

/// <summary>
/// Typed implementation of <see cref="IChartRunner"/> — closes over
/// <typeparamref name="TEntity"/> so the dashboard render path dispatches by
/// query name without reflection at request time. Delegates the actual
/// grouping to <see cref="IQueryEngine{TEntity}.ExecuteGroupedAsync"/>, which
/// already knows how to apply common filters, materialise per-group counts
/// and clamp the result set against <c>MaxGroupCount</c>.
/// </summary>
internal sealed class ChartRunner<TEntity>(
    string name,
    IQueryableSource<TEntity> source,
    IQueryEngine<TEntity> engine) : IChartRunner
    where TEntity : class
{
    private readonly IQueryableSource<TEntity> _source = source;
    private readonly IQueryEngine<TEntity> _engine = engine;

    public string Name { get; } = name;

    public async Task<ChartRunnerResult?> ExecuteAsync(
        string groupBy,
        AggregateFunction aggregation,
        string? field,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupBy);

        if (aggregation != AggregateFunction.Count)
        {
            // Sum / Avg / Min / Max with field aggregation ship in a follow-up
            // slice — they need a typed group+aggregate expression tree
            // (mirrors QueryAggregateRunner B3-2ter, with an extra GroupBy
            // axis). Returning null lets the caller surface a dedicated
            // Widget:Unavailable.* reason instead of throwing.
            return null;
        }

        QueryRequest request = new() { GroupBy = groupBy };

        GroupedResult<TEntity> result = await _engine
            .ExecuteGroupedAsync(_source.GetQueryable(), request, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<ChartRunnerBucket> buckets = [..
            result.Groups.Select(g => new ChartRunnerBucket(g.Label, g.Count))];

        return new ChartRunnerResult(buckets);
    }
}
