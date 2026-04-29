using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Granit.QueryEngine.Filtering;
using Microsoft.EntityFrameworkCore;

namespace Granit.Analytics.EntityFrameworkCore.Internal;

/// <summary>
/// Runs an ungrouped <see cref="AggregateFunction"/> (Count / Sum / Avg /
/// Min / Max) against an <see cref="IQueryable{T}"/> via EF Core's typed
/// async aggregate operators. Lives in the EF Core layer so the
/// <c>Granit.Analytics.Endpoints</c> assembly can host the runner without
/// taking a direct <see cref="Microsoft.EntityFrameworkCore"/> dependency
/// (architecture rule: endpoints must use abstractions, not EF Core).
/// </summary>
/// <remarks>
/// <para>
/// Empty-set semantics shared with <c>MetricExecutor</c> (locked by tests #1374):
/// </para>
/// <list type="bullet">
///   <item><c>Count</c> empty → <c>0</c>.</item>
///   <item><c>Sum</c> empty → <c>0</c> (sum-of-empty identity).</item>
///   <item><c>Avg</c> / <c>Min</c> / <c>Max</c> empty → <see langword="null"/>.</item>
/// </list>
/// <para>
/// Configuration errors (unknown field, unsupported field type) throw —
/// <c>IDashboardRenderer</c>'s per-widget error isolation surfaces those as
/// <c>Error</c> envelopes per ADR-039 §3.c.
/// </para>
/// </remarks>
internal static class QueryAggregateExecutor
{
    public static async Task<decimal?> ExecuteCountAsync<TEntity>(
        IQueryable<TEntity> source, CancellationToken cancellationToken)
        where TEntity : class
    {
        int count = await source.CountAsync(cancellationToken).ConfigureAwait(false);
        return count;
    }

    public static async Task<decimal?> ExecuteSumAsync<TEntity>(
        IQueryable<TEntity> source, PropertyInfo prop, Type underlying, CancellationToken ct)
        where TEntity : class
    {
        // Sum semantics: empty set = 0 (sum-of-empty identity). Locked by tests
        // #1374 for the metric path and inherited here so dashboard KPIs and
        // inline metrics never disagree on what "no rows" means for a Sum.
        if (underlying == typeof(int))
        {
            int? r = await source.SumAsync(BuildSelector<TEntity, int>(prop), ct).ConfigureAwait(false);
            return r ?? 0;
        }
        if (underlying == typeof(long))
        {
            long? r = await source.SumAsync(BuildSelector<TEntity, long>(prop), ct).ConfigureAwait(false);
            return r ?? 0L;
        }
        if (underlying == typeof(decimal))
        {
            decimal? r = await source.SumAsync(BuildSelector<TEntity, decimal>(prop), ct).ConfigureAwait(false);
            return r ?? 0m;
        }
        if (underlying == typeof(double))
        {
            double? r = await source.SumAsync(BuildSelector<TEntity, double>(prop), ct).ConfigureAwait(false);
            return r.HasValue ? Convert.ToDecimal(r.Value, CultureInfo.InvariantCulture) : 0m;
        }

        throw NotSupported(prop, underlying, AggregateFunction.Sum);
    }

    public static async Task<decimal?> ExecuteAvgAsync<TEntity>(
        IQueryable<TEntity> source, PropertyInfo prop, Type underlying, CancellationToken ct)
        where TEntity : class
    {
        // Avg semantics: empty set = null ("no data"). NEVER zero — division by
        // zero is undefined and the frontend renders "—".
        if (underlying == typeof(int))
        {
            double? r = await source.AverageAsync(BuildSelector<TEntity, int>(prop), ct).ConfigureAwait(false);
            return r.HasValue ? Convert.ToDecimal(r.Value, CultureInfo.InvariantCulture) : null;
        }
        if (underlying == typeof(long))
        {
            double? r = await source.AverageAsync(BuildSelector<TEntity, long>(prop), ct).ConfigureAwait(false);
            return r.HasValue ? Convert.ToDecimal(r.Value, CultureInfo.InvariantCulture) : null;
        }
        if (underlying == typeof(decimal))
        {
            decimal? r = await source.AverageAsync(BuildSelector<TEntity, decimal>(prop), ct).ConfigureAwait(false);
            return r;
        }
        if (underlying == typeof(double))
        {
            double? r = await source.AverageAsync(BuildSelector<TEntity, double>(prop), ct).ConfigureAwait(false);
            return r.HasValue ? Convert.ToDecimal(r.Value, CultureInfo.InvariantCulture) : null;
        }

        throw NotSupported(prop, underlying, AggregateFunction.Avg);
    }

    public static async Task<decimal?> ExecuteMinMaxAsync<TEntity>(
        IQueryable<TEntity> source, PropertyInfo prop, Type underlying, bool isMax, CancellationToken ct)
        where TEntity : class
    {
        // Min/Max semantics: empty set = null. Project to the value type then
        // call the generic IQueryable<T?>.MinAsync / MaxAsync — no per-type
        // dispatch needed for the EF Core call, only for the result coercion
        // to decimal.
        if (underlying == typeof(int))
        {
            int? r = await ExecuteMinMaxTypedAsync<TEntity, int>(source, BuildSelector<TEntity, int>(prop), isMax, ct).ConfigureAwait(false);
            return r;
        }
        if (underlying == typeof(long))
        {
            long? r = await ExecuteMinMaxTypedAsync<TEntity, long>(source, BuildSelector<TEntity, long>(prop), isMax, ct).ConfigureAwait(false);
            return r;
        }
        if (underlying == typeof(decimal))
        {
            decimal? r = await ExecuteMinMaxTypedAsync<TEntity, decimal>(source, BuildSelector<TEntity, decimal>(prop), isMax, ct).ConfigureAwait(false);
            return r;
        }
        if (underlying == typeof(double))
        {
            double? r = await ExecuteMinMaxTypedAsync<TEntity, double>(source, BuildSelector<TEntity, double>(prop), isMax, ct).ConfigureAwait(false);
            return r.HasValue ? Convert.ToDecimal(r.Value, CultureInfo.InvariantCulture) : null;
        }

        throw NotSupported(prop, underlying, isMax ? AggregateFunction.Max : AggregateFunction.Min);
    }

    private static Task<TValue?> ExecuteMinMaxTypedAsync<TEntity, TValue>(
        IQueryable<TEntity> q,
        Expression<Func<TEntity, TValue?>> selector,
        bool isMax,
        CancellationToken ct)
        where TEntity : class
        where TValue : struct =>
        isMax
            ? q.Select(selector).MaxAsync(ct)
            : q.Select(selector).MinAsync(ct);

    /// <summary>
    /// Builds <c>x =&gt; (TValue?)x.{Field}</c>. Lifts non-nullable value types
    /// to <see cref="Nullable{T}"/> so EF Core's typed Sum / Avg / Min / Max
    /// overloads can return nullable results — the only way to express "no
    /// rows" for Avg / Min / Max in SQL.
    /// </summary>
    private static Expression<Func<TEntity, TValue?>> BuildSelector<TEntity, TValue>(PropertyInfo prop)
        where TValue : struct
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
