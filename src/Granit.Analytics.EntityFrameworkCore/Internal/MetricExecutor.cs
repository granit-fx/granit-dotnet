using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using Granit.Analytics.Diagnostics;
using Granit.Analytics.Metrics;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Granit.QueryEngine.Filtering;
using Microsoft.EntityFrameworkCore;

namespace Granit.Analytics.EntityFrameworkCore.Internal;

/// <summary>
/// Default <see cref="IMetricExecutor{TEntity, TValue}"/> implementation. Runs the
/// metric through <see cref="IQueryEngine{TEntity}.BuildFilteredQuery"/> so KPIs and
/// grids share the same filter logic, then dispatches to the right EF Core aggregate
/// per <typeparamref name="TValue"/>.
/// </summary>
/// <remarks>
/// Empty-set semantics (locked by tests in story #1374):
/// <list type="bullet">
///   <item><c>Count</c> over empty set → <c>0</c>.</item>
///   <item><c>Sum</c> over empty set → <c>default(TValue)</c> (e.g. <c>0m</c> for decimal).</item>
///   <item><c>Avg</c> / <c>Min</c> / <c>Max</c> over empty set → <c>null</c> (semantic "no data").</item>
/// </list>
/// </remarks>
internal sealed class MetricExecutor<TEntity, TValue>(
    IQueryEngine<TEntity> queryEngine,
    IServiceProvider? serviceProvider = null,
    AnalyticsMetrics? metrics = null,
    ICurrentTenant? currentTenant = null) : IMetricExecutor<TEntity, TValue>
    where TEntity : class
    where TValue : struct
{
    private readonly IServiceProvider? _serviceProvider = serviceProvider;
    public async Task<TValue?> ExecuteAsync(
        MetricDefinition<TEntity, TValue> metric,
        IQueryable<TEntity> source,
        QueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metric);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);

        using Activity? activity = AnalyticsActivitySource.Source.StartActivity(AnalyticsActivitySource.MetricExecute);
        activity?.SetTag("metric.name", metric.Name);
        activity?.SetTag("metric.aggregation", metric.Aggregation.ToString());
        long startTimestamp = Stopwatch.GetTimestamp();

        IQueryable<TEntity> filtered = queryEngine.BuildFilteredQuery(source, request);

        // Compose the metric's intrinsic predicate (e.g. "Status == Open" on an
        // UnpaidInvoiceCount) AFTER the user filters, so admin-supplied filters
        // (period, customer, …) compose with AND semantics on top of the framework's
        // multi-tenant + soft-delete filters that BuildFilteredQuery already applied.
        if (metric.BaseFilter is not null)
        {
            filtered = filtered.Where(metric.BaseFilter);
        }

        // Joined-metric dispatch — metrics deriving from JoinedMetricDefinition<,,>
        // surface IJoinedMetricProjector<TEntity, TValue>. The executor resolves the
        // joined source from DI via the metric's Project method, then aggregates the
        // resulting IQueryable<TValue?> with the same per-type EF Core dispatch and
        // empty-set semantics as the single-table path. Count is unsupported because
        // the projection is the unit of aggregation — counting joined rows requires
        // a regular MetricDefinition.
        TValue? result;
        if (metric is IJoinedMetricProjector<TEntity, TValue> projector)
        {
            if (_serviceProvider is null)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant(
                        $"Joined metric '{metric.Name}' requires the MetricExecutor to be constructed with an IServiceProvider so the joined source can be resolved at request time. The DI registration in AddGranitAnalyticsEntityFrameworkCore() supplies one automatically; tests that build the executor directly must pass an IServiceProvider too."));
            }

            IQueryable<TValue?> projection = projector.ProjectFromServiceProvider(filtered, _serviceProvider);
            result = metric.Aggregation switch
            {
                AggregateFunction.Sum => await SumProjectionAsync(projection, cancellationToken).ConfigureAwait(false),
                AggregateFunction.Avg => await AvgProjectionAsync(projection, cancellationToken).ConfigureAwait(false),
                AggregateFunction.Min => await projection.MinAsync(cancellationToken).ConfigureAwait(false),
                AggregateFunction.Max => await projection.MaxAsync(cancellationToken).ConfigureAwait(false),
                AggregateFunction.Count => throw new NotSupportedException(
                    FormattableString.Invariant(
                        $"Joined metric '{metric.Name}' uses Aggregation = Count, which is not supported on JoinedMetricDefinition. ")
                    + "Counting joined rows is meaningful as a regular MetricDefinition over the base entity instead."),
                _ => throw new NotSupportedException(
                    FormattableString.Invariant($"Aggregation '{metric.Aggregation}' is not supported.")),
            };
        }
        else
        {
            result = metric.Aggregation switch
            {
                AggregateFunction.Count => await ExecuteCountAsync(filtered, cancellationToken).ConfigureAwait(false),
                AggregateFunction.Sum => await ExecuteSumAsync(filtered, RequireSelector(metric), cancellationToken).ConfigureAwait(false),
                AggregateFunction.Avg => await ExecuteAvgAsync(filtered, RequireSelector(metric), cancellationToken).ConfigureAwait(false),
                AggregateFunction.Min => await ExecuteMinAsync(filtered, RequireSelector(metric), cancellationToken).ConfigureAwait(false),
                AggregateFunction.Max => await ExecuteMaxAsync(filtered, RequireSelector(metric), cancellationToken).ConfigureAwait(false),
                _ => throw new NotSupportedException(
                    FormattableString.Invariant($"Aggregation '{metric.Aggregation}' is not supported.")),
            };
        }

        RecordMetrics(metric, result.HasValue, Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds);
        return result;
    }

    private static async Task<TValue?> SumProjectionAsync(IQueryable<TValue?> projection, CancellationToken ct)
    {
        // Sum semantics on the projected IQueryable<TValue?> — same per-type dispatch
        // and empty-set defaults as the single-table path.
        if (typeof(TValue) == typeof(decimal))
        {
            decimal? r = await projection.Cast<decimal?>().SumAsync(ct).ConfigureAwait(false);
            return (TValue?)(object?)(r ?? 0m);
        }
        if (typeof(TValue) == typeof(int))
        {
            int? r = await projection.Cast<int?>().SumAsync(ct).ConfigureAwait(false);
            return (TValue?)(object?)(r ?? 0);
        }
        if (typeof(TValue) == typeof(long))
        {
            long? r = await projection.Cast<long?>().SumAsync(ct).ConfigureAwait(false);
            return (TValue?)(object?)(r ?? 0L);
        }
        if (typeof(TValue) == typeof(double))
        {
            double? r = await projection.Cast<double?>().SumAsync(ct).ConfigureAwait(false);
            return (TValue?)(object?)(r ?? 0.0);
        }
        throw NotSupported(nameof(AggregateFunction.Sum));
    }

    private static async Task<TValue?> AvgProjectionAsync(IQueryable<TValue?> projection, CancellationToken ct)
    {
        // Avg over a projected IQueryable<TValue?> — empty-set returns null per
        // the framework contract.
        if (typeof(TValue) == typeof(decimal))
        {
            decimal? r = await projection.Cast<decimal?>().AverageAsync(ct).ConfigureAwait(false);
            return (TValue?)(object?)r;
        }
        if (typeof(TValue) == typeof(int))
        {
            double? r = await projection.Cast<int?>().AverageAsync(ct).ConfigureAwait(false);
            return r.HasValue ? (TValue?)(object?)(int)Math.Round(r.Value) : null;
        }
        if (typeof(TValue) == typeof(long))
        {
            double? r = await projection.Cast<long?>().AverageAsync(ct).ConfigureAwait(false);
            return r.HasValue ? (TValue?)(object?)(long)Math.Round(r.Value) : null;
        }
        if (typeof(TValue) == typeof(double))
        {
            double? r = await projection.Cast<double?>().AverageAsync(ct).ConfigureAwait(false);
            return (TValue?)(object?)r;
        }
        throw NotSupported(nameof(AggregateFunction.Avg));
    }

    private static Expression<Func<TEntity, TValue?>> RequireSelector(MetricDefinition<TEntity, TValue> metric) =>
        metric.Selector ?? throw new InvalidOperationException(
            FormattableString.Invariant(
                $"Metric '{metric.Name}' uses aggregation '{metric.Aggregation}' but does not declare a Selector. Selector is required for Sum / Avg / Min / Max."));

    private static async Task<TValue?> ExecuteCountAsync(IQueryable<TEntity> filtered, CancellationToken ct)
    {
        int count = await filtered.CountAsync(ct).ConfigureAwait(false);
        // TValue is a value type that can hold an int (caller declared MetricDefinition<TEntity, int|long|decimal|double>).
        return (TValue)Convert.ChangeType(count, typeof(TValue), CultureInfo.InvariantCulture);
    }

    private static async Task<TValue?> ExecuteSumAsync(
        IQueryable<TEntity> filtered,
        Expression<Func<TEntity, TValue?>> selector,
        CancellationToken ct)
    {
        // Sum semantics: empty set returns default(TValue) (= 0 for numerics — mathematical sum-of-empty).
        if (typeof(TValue) == typeof(decimal))
        {
            var typed = Expression.Lambda<Func<TEntity, decimal?>>(selector.Body, selector.Parameters);
            decimal? r = await filtered.SumAsync(typed, ct).ConfigureAwait(false);
            return (TValue?)(object?)(r ?? 0m);
        }
        if (typeof(TValue) == typeof(int))
        {
            var typed = Expression.Lambda<Func<TEntity, int?>>(selector.Body, selector.Parameters);
            int? r = await filtered.SumAsync(typed, ct).ConfigureAwait(false);
            return (TValue?)(object?)(r ?? 0);
        }
        if (typeof(TValue) == typeof(long))
        {
            var typed = Expression.Lambda<Func<TEntity, long?>>(selector.Body, selector.Parameters);
            long? r = await filtered.SumAsync(typed, ct).ConfigureAwait(false);
            return (TValue?)(object?)(r ?? 0L);
        }
        if (typeof(TValue) == typeof(double))
        {
            var typed = Expression.Lambda<Func<TEntity, double?>>(selector.Body, selector.Parameters);
            double? r = await filtered.SumAsync(typed, ct).ConfigureAwait(false);
            return (TValue?)(object?)(r ?? 0.0);
        }
        throw NotSupported(nameof(AggregateFunction.Sum));
    }

    private static async Task<TValue?> ExecuteAvgAsync(
        IQueryable<TEntity> filtered,
        Expression<Func<TEntity, TValue?>> selector,
        CancellationToken ct)
    {
        // Avg semantics: empty set returns null ("no data" — never zero, never an exception).
        if (typeof(TValue) == typeof(decimal))
        {
            var typed = Expression.Lambda<Func<TEntity, decimal?>>(selector.Body, selector.Parameters);
            decimal? r = await filtered.AverageAsync(typed, ct).ConfigureAwait(false);
            return (TValue?)(object?)r;
        }
        if (typeof(TValue) == typeof(int))
        {
            // EF Core: Average over int? returns double?. Coalesce to int? per declared TValue.
            var typed = Expression.Lambda<Func<TEntity, int?>>(selector.Body, selector.Parameters);
            double? r = await filtered.AverageAsync(typed, ct).ConfigureAwait(false);
            return r.HasValue ? (TValue?)(object?)(int)Math.Round(r.Value) : null;
        }
        if (typeof(TValue) == typeof(long))
        {
            var typed = Expression.Lambda<Func<TEntity, long?>>(selector.Body, selector.Parameters);
            double? r = await filtered.AverageAsync(typed, ct).ConfigureAwait(false);
            return r.HasValue ? (TValue?)(object?)(long)Math.Round(r.Value) : null;
        }
        if (typeof(TValue) == typeof(double))
        {
            var typed = Expression.Lambda<Func<TEntity, double?>>(selector.Body, selector.Parameters);
            double? r = await filtered.AverageAsync(typed, ct).ConfigureAwait(false);
            return (TValue?)(object?)r;
        }
        throw NotSupported(nameof(AggregateFunction.Avg));
    }

    private static async Task<TValue?> ExecuteMinAsync(
        IQueryable<TEntity> filtered,
        Expression<Func<TEntity, TValue?>> selector,
        CancellationToken ct)
    {
        // Min/Max use the generic IQueryable<T?>.MinAsync / MaxAsync — no per-type dispatch needed.
        // EF Core projects to TValue? then translates Min/Max in SQL.
        return await filtered.Select(selector).MinAsync(ct).ConfigureAwait(false);
    }

    private static async Task<TValue?> ExecuteMaxAsync(
        IQueryable<TEntity> filtered,
        Expression<Func<TEntity, TValue?>> selector,
        CancellationToken ct) =>
        await filtered.Select(selector).MaxAsync(ct).ConfigureAwait(false);

    private static NotSupportedException NotSupported(string aggregation) =>
        new(FormattableString.Invariant(
            $"Aggregation '{aggregation}' on TValue '{typeof(TValue).Name}' is not supported. Supported types: int, long, decimal, double."));

    private void RecordMetrics(MetricDefinition<TEntity, TValue> metric, bool hasValue, double durationSeconds)
    {
        if (metrics is null)
        {
            return;
        }

        string? tenantId = currentTenant is { IsAvailable: true } ? currentTenant.Id?.ToString() : null;
        string aggregation = metric.Aggregation.ToString();

        metrics.RecordMetricExecuted(tenantId, metric.Name, aggregation);
        metrics.RecordMetricDuration(tenantId, metric.Name, aggregation, durationSeconds);

        if (!hasValue && metric.Aggregation is AggregateFunction.Avg or AggregateFunction.Min or AggregateFunction.Max)
        {
            metrics.RecordEmptySet(tenantId, metric.Name, aggregation);
        }
    }
}
