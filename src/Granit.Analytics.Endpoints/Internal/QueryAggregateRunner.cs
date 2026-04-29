using System.Reflection;
using Granit.Analytics.EntityFrameworkCore.Internal;
using Granit.QueryEngine;
using Granit.QueryEngine.Filtering;

namespace Granit.Analytics.Endpoints.Internal;

/// <summary>
/// Typed implementation of <see cref="IQueryAggregateRunner"/> — closes over
/// <typeparamref name="TEntity"/> so the dashboard render path dispatches by
/// query name without reflection at request time. Field-name → property
/// dispatch for Sum / Avg / Min / Max happens once per call (cost is the
/// reflective property lookup; the actual EF Core aggregation is delegated
/// to <see cref="QueryAggregateExecutor"/> which lives in
/// <c>Granit.Analytics.EntityFrameworkCore</c> — the runner itself stays
/// EF-Core-free per the architecture rule that endpoints must not depend
/// on EF Core directly).
/// </summary>
/// <remarks>
/// Wraps the entity's <see cref="IQueryableSource{TEntity}"/>. Dashboard
/// filters layer on top via <see cref="IQueryEngine{TEntity}.BuildFilteredQuery"/>
/// — same filter pipeline as the admin grid, so a <c>"Status=Open"</c>
/// dashboard filter narrows the KPI tile the same way it narrows the table
/// next to it.
/// </remarks>
internal sealed class QueryAggregateRunner<TEntity>(
    string name,
    IQueryableSource<TEntity> source,
    IQueryEngine<TEntity> engine) : IQueryAggregateRunner
    where TEntity : class
{
    private readonly IQueryableSource<TEntity> _source = source;
    private readonly IQueryEngine<TEntity> _engine = engine;

    public string Name { get; } = name;

    public async Task<decimal?> ExecuteAsync(
        AggregateFunction aggregation,
        string? field,
        IReadOnlyDictionary<string, string>? dashboardFilters,
        CancellationToken cancellationToken)
    {
        IQueryable<TEntity> rawQueryable = _source.GetQueryable();

        // Layer dashboard filters via the QueryEngine pipeline so a
        // "Status=Open" dashboard filter narrows the KPI tile the same way
        // the admin grid does.
        QueryRequest filterRequest = new()
        {
            Filter = DashboardFilterTranslator.ToQueryRequestFilter(dashboardFilters),
        };
        IQueryable<TEntity> queryable = filterRequest.Filter is { Count: > 0 }
            ? _engine.BuildFilteredQuery(rawQueryable, filterRequest)
            : rawQueryable;

        if (aggregation == AggregateFunction.Count)
        {
            return await QueryAggregateExecutor.ExecuteCountAsync(queryable, cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(field))
        {
            throw new ArgumentException(
                $"Aggregation '{aggregation}' on query '{Name}' requires a Field — Field was null or empty.",
                nameof(field));
        }

        PropertyInfo prop = typeof(TEntity).GetProperty(
            field,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            ?? throw new ArgumentException(
                $"Field '{field}' not found on entity '{typeof(TEntity).Name}' for query '{Name}'.",
                nameof(field));

        Type underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

        return aggregation switch
        {
            AggregateFunction.Sum => await QueryAggregateExecutor.ExecuteSumAsync(queryable, prop, underlying, cancellationToken).ConfigureAwait(false),
            AggregateFunction.Avg => await QueryAggregateExecutor.ExecuteAvgAsync(queryable, prop, underlying, cancellationToken).ConfigureAwait(false),
            AggregateFunction.Min => await QueryAggregateExecutor.ExecuteMinMaxAsync(queryable, prop, underlying, isMax: false, cancellationToken).ConfigureAwait(false),
            AggregateFunction.Max => await QueryAggregateExecutor.ExecuteMinMaxAsync(queryable, prop, underlying, isMax: true, cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException(
                FormattableString.Invariant($"Aggregation '{aggregation}' is not supported.")),
        };
    }
}
