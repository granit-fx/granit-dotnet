using System.Globalization;
using System.Reflection;
using Granit.Analytics.EntityFrameworkCore.Internal;
using Granit.QueryEngine;
using Granit.QueryEngine.Filtering;

namespace Granit.Analytics.Endpoints.Internal;

/// <summary>
/// Typed implementation of <see cref="IChartRunner"/> — closes over
/// <typeparamref name="TEntity"/> so the dashboard render path dispatches by
/// query name without reflection at request time.
/// </summary>
/// <remarks>
/// <para>
/// Two paths, picked by <see cref="AggregateFunction"/>:
/// </para>
/// <list type="bullet">
///   <item><b>Count</b> — delegates to <c>IQueryEngine&lt;TEntity&gt;.ExecuteGroupedAsync</c>;
///         grouping happens in SQL via the existing QueryEngine pipeline.</item>
///   <item><b>Sum / Avg / Min / Max</b> — builds a typed
///         <c>GroupBy(...).Select(...)</c> expression tree via
///         <see cref="GroupAggregateExecutor"/> and lets EF Core push the
///         aggregation to SQL. Same filter pipeline / multi-tenancy as the
///         admin grid (via <c>IQueryEngine.BuildFilteredQuery</c>); same
///         empty-set semantics as <c>MetricExecutor</c>.</item>
/// </list>
/// </remarks>
internal sealed class ChartRunner<TEntity>(
    string name,
    IQueryableSource<TEntity> source,
    IQueryEngine<TEntity> engine) : IChartRunner
    where TEntity : class
{
    private readonly IQueryableSource<TEntity> _source = source;
    private readonly IQueryEngine<TEntity> _engine = engine;

    public string Name { get; } = name;

    public async Task<ChartRunnerResult> ExecuteAsync(
        string groupBy,
        AggregateFunction aggregation,
        string? field,
        IReadOnlyDictionary<string, string>? dashboardFilters,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupBy);

        if (aggregation == AggregateFunction.Count)
        {
            return await ExecuteCountAsync(groupBy, dashboardFilters, cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(field))
        {
            throw new ArgumentException(
                $"Aggregation '{aggregation}' on chart for query '{Name}' requires a Field — Field was null or empty.",
                nameof(field));
        }

        return await ExecuteNumericAsync(groupBy, aggregation, field, dashboardFilters, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ChartRunnerResult> ExecuteCountAsync(
        string groupBy, IReadOnlyDictionary<string, string>? dashboardFilters, CancellationToken ct)
    {
        QueryRequest request = new()
        {
            GroupBy = groupBy,
            Filter = DashboardFilterTranslator.ToQueryRequestFilter(dashboardFilters),
        };

        GroupedResult<TEntity> result = await _engine
            .ExecuteGroupedAsync(_source.GetQueryable(), request, ct)
            .ConfigureAwait(false);

        IReadOnlyList<ChartRunnerBucket> buckets = [..
            result.Groups.Select(g => new ChartRunnerBucket(g.Label, g.Count))];

        return new ChartRunnerResult(buckets);
    }

    private async Task<ChartRunnerResult> ExecuteNumericAsync(
        string groupBy, AggregateFunction aggregation, string field,
        IReadOnlyDictionary<string, string>? dashboardFilters, CancellationToken ct)
    {
        PropertyInfo groupProp = ResolveProperty(groupBy, nameof(groupBy));
        PropertyInfo valueProp = ResolveProperty(field, nameof(field));

        Type valueUnderlying = Nullable.GetUnderlyingType(valueProp.PropertyType) ?? valueProp.PropertyType;

        if (!GroupAggregateExecutor.IsSupportedValueType(valueUnderlying))
        {
            throw new NotSupportedException(FormattableString.Invariant(
                $"Aggregation '{aggregation}' on field '{valueProp.Name}' (type '{valueUnderlying.Name}') is not supported. Supported types: int, long, decimal, double."));
        }

        // Apply filter pipeline (multi-tenancy, soft-delete, dashboard filters).
        // Same set of rows the admin grid would see.
        QueryRequest request = new()
        {
            Filter = DashboardFilterTranslator.ToQueryRequestFilter(dashboardFilters),
        };
        IQueryable<TEntity> filtered = _engine.BuildFilteredQuery(_source.GetQueryable(), request);

        List<GroupAggregateExecutor.GroupBucket> raw = await GroupAggregateExecutor
            .ExecuteAsync(filtered, groupProp, valueProp, valueUnderlying, aggregation, ct)
            .ConfigureAwait(false);

        IReadOnlyList<ChartRunnerBucket> buckets = [..
            raw.Select(b => new ChartRunnerBucket(LabelOf(b.Key), b.Value))];

        return new ChartRunnerResult(buckets);
    }

    private static PropertyInfo ResolveProperty(string fieldName, string paramName)
    {
        PropertyInfo? prop = typeof(TEntity).GetProperty(
            fieldName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        return prop ?? throw new ArgumentException(
            $"Field '{fieldName}' not found on entity '{typeof(TEntity).Name}'.",
            paramName);
    }

    private static string LabelOf(object? key) =>
        key switch
        {
            null => "(null)",
            string s => s,
            _ => Convert.ToString(key, CultureInfo.InvariantCulture) ?? "(null)",
        };
}
