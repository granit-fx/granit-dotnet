using System.Globalization;
using System.Reflection;
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
///         grouping happens in SQL.</item>
///   <item><b>Sum / Avg / Min / Max</b> — streams the filtered entity set
///         through <c>ExecuteStreamAsync</c> (capped by <c>MaxStreamSize</c>)
///         and computes the per-group aggregate in memory. Acceptable for
///         dashboard tiles which are bounded data products; future
///         optimisation could push the aggregation to SQL via dynamic
///         expression trees.</item>
/// </list>
/// <para>
/// Sharing <c>ExecuteStreamAsync</c> means the chart tile honours the same
/// filter pipeline / multi-tenancy / soft-delete contract as the admin grid
/// — the same rows the table sees are the same rows the chart aggregates.
/// </para>
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
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupBy);

        if (aggregation == AggregateFunction.Count)
        {
            return await ExecuteCountAsync(groupBy, cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(field))
        {
            throw new ArgumentException(
                $"Aggregation '{aggregation}' on chart for query '{Name}' requires a Field — Field was null or empty.",
                nameof(field));
        }

        return await ExecuteNumericAsync(groupBy, aggregation, field, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ChartRunnerResult> ExecuteCountAsync(string groupBy, CancellationToken ct)
    {
        QueryRequest request = new() { GroupBy = groupBy };

        GroupedResult<TEntity> result = await _engine
            .ExecuteGroupedAsync(_source.GetQueryable(), request, ct)
            .ConfigureAwait(false);

        IReadOnlyList<ChartRunnerBucket> buckets = [..
            result.Groups.Select(g => new ChartRunnerBucket(g.Label, g.Count))];

        return new ChartRunnerResult(buckets);
    }

    private async Task<ChartRunnerResult> ExecuteNumericAsync(
        string groupBy, AggregateFunction aggregation, string field, CancellationToken ct)
    {
        PropertyInfo groupProp = ResolveProperty(groupBy, nameof(groupBy));
        PropertyInfo valueProp = ResolveProperty(field, nameof(field));

        Type valueUnderlying = Nullable.GetUnderlyingType(valueProp.PropertyType) ?? valueProp.PropertyType;
        EnsureSupportedNumericType(valueProp, valueUnderlying, aggregation);

        // Stream the filtered set through the QueryEngine pipeline (multi-tenancy,
        // soft-delete, MaxStreamSize cap apply) and aggregate in memory. Acceptable
        // for dashboard tiles; pushing the aggregate to SQL would need a typed
        // GroupBy + Select expression tree per (TKey, TValue) primitive pair.
        QueryRequest request = new();
        Dictionary<object, AggregateAccumulator> groups = new(GroupKeyComparer.Instance);

        await foreach (TEntity entity in _engine
            .ExecuteStreamAsync(_source.GetQueryable(), request, ct)
            .ConfigureAwait(false))
        {
            object? rawKey = groupProp.GetValue(entity);
            object? rawValue = valueProp.GetValue(entity);

            object key = rawKey ?? GroupKeyComparer.NullSentinel;
            if (!groups.TryGetValue(key, out AggregateAccumulator? acc))
            {
                acc = new AggregateAccumulator(rawKey);
                groups[key] = acc;
            }

            if (rawValue is not null)
            {
                acc.Add(Convert.ToDecimal(rawValue, CultureInfo.InvariantCulture));
            }
        }

        IReadOnlyList<ChartRunnerBucket> buckets = [..
            groups.Values.Select(acc => new ChartRunnerBucket(
                Label: LabelOf(acc.RawKey),
                Value: acc.Compute(aggregation)))];

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

    private static void EnsureSupportedNumericType(PropertyInfo prop, Type underlying, AggregateFunction aggregation)
    {
        if (underlying == typeof(int) || underlying == typeof(long)
            || underlying == typeof(decimal) || underlying == typeof(double))
        {
            return;
        }

        throw new NotSupportedException(FormattableString.Invariant(
            $"Aggregation '{aggregation}' on field '{prop.Name}' (type '{underlying.Name}') is not supported. Supported types: int, long, decimal, double."));
    }

    private static string LabelOf(object? key) =>
        key switch
        {
            null => "(null)",
            string s => s,
            _ => Convert.ToString(key, CultureInfo.InvariantCulture) ?? "(null)",
        };

    private sealed class AggregateAccumulator(object? rawKey)
    {
        private readonly List<decimal> _values = [];

        public object? RawKey { get; } = rawKey;

        public void Add(decimal value) => _values.Add(value);

        public decimal? Compute(AggregateFunction aggregation) =>
            aggregation switch
            {
                // Sum-of-empty is 0 (mathematical identity, matches MetricExecutor).
                AggregateFunction.Sum => _values.Count == 0 ? 0m : _values.Sum(),

                // Avg / Min / Max over a group with no usable values surface as
                // null — locked semantics shared with MetricExecutor #1374.
                AggregateFunction.Avg => _values.Count == 0 ? null : _values.Average(),
                AggregateFunction.Min => _values.Count == 0 ? null : _values.Min(),
                AggregateFunction.Max => _values.Count == 0 ? null : _values.Max(),

                _ => throw new NotSupportedException(
                    FormattableString.Invariant($"Aggregation '{aggregation}' is not supported.")),
            };
    }

    private sealed class GroupKeyComparer : IEqualityComparer<object>
    {
        public static readonly GroupKeyComparer Instance = new();
        public static readonly object NullSentinel = new();

        public new bool Equals(object? x, object? y) => object.Equals(x, y);

        public int GetHashCode(object obj) => obj.GetHashCode();
    }
}
