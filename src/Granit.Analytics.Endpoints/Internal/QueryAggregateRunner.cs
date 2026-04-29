using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Granit.QueryEngine;
using Granit.QueryEngine.Filtering;
using Microsoft.EntityFrameworkCore;

namespace Granit.Analytics.Endpoints.Internal;

/// <summary>
/// Typed implementation of <see cref="IQueryAggregateRunner"/> — closes over
/// <typeparamref name="TEntity"/> so the dashboard render path dispatches by
/// query name without reflection at request time. Field-name → property
/// dispatch for Sum / Avg / Min / Max happens once per call (cost is the
/// reflective property lookup; the LINQ tree itself is materialised by EF Core
/// without further runtime branching).
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

        // Layer dashboard filters via the QueryEngine pipeline so a "Status=Open"
        // dashboard filter narrows the KPI tile the same way the admin grid does.
        QueryRequest filterRequest = new()
        {
            Filter = DashboardFilterTranslator.ToQueryRequestFilter(dashboardFilters),
        };
        IQueryable<TEntity> queryable = filterRequest.Filter is { Count: > 0 }
            ? _engine.BuildFilteredQuery(rawQueryable, filterRequest)
            : rawQueryable;

        if (aggregation == AggregateFunction.Count)
        {
            int count = await queryable.CountAsync(cancellationToken).ConfigureAwait(false);
            return count;
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
            AggregateFunction.Sum => await ExecuteSumAsync(queryable, prop, underlying, cancellationToken).ConfigureAwait(false),
            AggregateFunction.Avg => await ExecuteAvgAsync(queryable, prop, underlying, cancellationToken).ConfigureAwait(false),
            AggregateFunction.Min => await ExecuteMinMaxAsync(queryable, prop, underlying, isMax: false, cancellationToken).ConfigureAwait(false),
            AggregateFunction.Max => await ExecuteMinMaxAsync(queryable, prop, underlying, isMax: true, cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException(
                FormattableString.Invariant($"Aggregation '{aggregation}' is not supported.")),
        };
    }

    private static async Task<decimal?> ExecuteSumAsync(
        IQueryable<TEntity> q, PropertyInfo prop, Type underlying, CancellationToken ct)
    {
        // Sum semantics: empty set = 0 (sum-of-empty identity). Locked by tests #1374
        // for the metric path and inherited here so dashboard KPIs and inline metrics
        // never disagree on what "no rows" means for a Sum.
        if (underlying == typeof(int))
        {
            int? r = await q.SumAsync(BuildSelector<int>(prop), ct).ConfigureAwait(false);
            return r ?? 0;
        }
        if (underlying == typeof(long))
        {
            long? r = await q.SumAsync(BuildSelector<long>(prop), ct).ConfigureAwait(false);
            return r ?? 0L;
        }
        if (underlying == typeof(decimal))
        {
            decimal? r = await q.SumAsync(BuildSelector<decimal>(prop), ct).ConfigureAwait(false);
            return r ?? 0m;
        }
        if (underlying == typeof(double))
        {
            double? r = await q.SumAsync(BuildSelector<double>(prop), ct).ConfigureAwait(false);
            return r.HasValue ? Convert.ToDecimal(r.Value, CultureInfo.InvariantCulture) : 0m;
        }

        throw NotSupported(prop, underlying, AggregateFunction.Sum);
    }

    private static async Task<decimal?> ExecuteAvgAsync(
        IQueryable<TEntity> q, PropertyInfo prop, Type underlying, CancellationToken ct)
    {
        // Avg semantics: empty set = null ("no data"). NEVER zero — division by zero
        // is undefined and the frontend renders "—".
        if (underlying == typeof(int))
        {
            double? r = await q.AverageAsync(BuildSelector<int>(prop), ct).ConfigureAwait(false);
            return r.HasValue ? Convert.ToDecimal(r.Value, CultureInfo.InvariantCulture) : null;
        }
        if (underlying == typeof(long))
        {
            double? r = await q.AverageAsync(BuildSelector<long>(prop), ct).ConfigureAwait(false);
            return r.HasValue ? Convert.ToDecimal(r.Value, CultureInfo.InvariantCulture) : null;
        }
        if (underlying == typeof(decimal))
        {
            decimal? r = await q.AverageAsync(BuildSelector<decimal>(prop), ct).ConfigureAwait(false);
            return r;
        }
        if (underlying == typeof(double))
        {
            double? r = await q.AverageAsync(BuildSelector<double>(prop), ct).ConfigureAwait(false);
            return r.HasValue ? Convert.ToDecimal(r.Value, CultureInfo.InvariantCulture) : null;
        }

        throw NotSupported(prop, underlying, AggregateFunction.Avg);
    }

    private static async Task<decimal?> ExecuteMinMaxAsync(
        IQueryable<TEntity> q, PropertyInfo prop, Type underlying, bool isMax, CancellationToken ct)
    {
        // Min/Max semantics: empty set = null. We project to the value type then
        // call the generic IQueryable<T?>.MinAsync / MaxAsync — no per-type dispatch
        // needed for the EF Core call, only for the result coercion to decimal.
        if (underlying == typeof(int))
        {
            int? r = await ExecuteMinMaxTypedAsync(q, BuildSelector<int>(prop), isMax, ct).ConfigureAwait(false);
            return r;
        }
        if (underlying == typeof(long))
        {
            long? r = await ExecuteMinMaxTypedAsync(q, BuildSelector<long>(prop), isMax, ct).ConfigureAwait(false);
            return r;
        }
        if (underlying == typeof(decimal))
        {
            decimal? r = await ExecuteMinMaxTypedAsync(q, BuildSelector<decimal>(prop), isMax, ct).ConfigureAwait(false);
            return r;
        }
        if (underlying == typeof(double))
        {
            double? r = await ExecuteMinMaxTypedAsync(q, BuildSelector<double>(prop), isMax, ct).ConfigureAwait(false);
            return r.HasValue ? Convert.ToDecimal(r.Value, CultureInfo.InvariantCulture) : null;
        }

        throw NotSupported(prop, underlying, isMax ? AggregateFunction.Max : AggregateFunction.Min);
    }

    private static Task<TValue?> ExecuteMinMaxTypedAsync<TValue>(
        IQueryable<TEntity> q,
        Expression<Func<TEntity, TValue?>> selector,
        bool isMax,
        CancellationToken ct)
        where TValue : struct =>
        isMax
            ? q.Select(selector).MaxAsync(ct)
            : q.Select(selector).MinAsync(ct);

    /// <summary>
    /// Builds <c>x =&gt; (TValue?)x.{Field}</c>. Lifts non-nullable value types to
    /// <see cref="Nullable{T}"/> so EF Core's typed Sum / Avg / Min / Max overloads
    /// can return nullable results — the only way to express "no rows" for
    /// Avg / Min / Max in SQL.
    /// </summary>
    private static Expression<Func<TEntity, TValue?>> BuildSelector<TValue>(PropertyInfo prop) where TValue : struct
    {
        ParameterExpression param = Expression.Parameter(typeof(TEntity), "x");
        Expression access = Expression.Property(param, prop);

        if (prop.PropertyType != typeof(TValue?))
        {
            access = Expression.Convert(access, typeof(TValue?));
        }

        return Expression.Lambda<Func<TEntity, TValue?>>(access, param);
    }

    private static NotSupportedException NotSupported(PropertyInfo prop, Type underlying, AggregateFunction aggregation) =>
        new(FormattableString.Invariant(
            $"Aggregation '{aggregation}' on field '{prop.Name}' (type '{underlying.Name}') is not supported. Supported types: int, long, decimal, double."));
}
