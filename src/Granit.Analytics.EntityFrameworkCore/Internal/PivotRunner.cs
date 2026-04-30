using System.Globalization;
using System.Reflection;
using Granit.Analytics.Internal;
using Granit.QueryEngine;
using Granit.QueryEngine.Filtering;

namespace Granit.Analytics.EntityFrameworkCore.Internal;

/// <summary>
/// Typed implementation of <see cref="IPivotRunner"/> — closes over
/// <typeparamref name="TEntity"/> so dispatch by query name stays
/// reflection-free. Streams the filtered entity set through
/// <see cref="IQueryEngine{TEntity}.ExecuteStreamAsync"/> (multi-tenancy,
/// soft-delete, MaxStreamSize cap apply) and accumulates each row into the
/// (row-tuple, column-tuple) bucket it belongs to.
/// </summary>
internal sealed class PivotRunner<TEntity>(
    string name,
    IQueryableSource<TEntity> source,
    IQueryEngine<TEntity> engine,
    QueryDefinition<TEntity> definition) : IPivotRunner
    where TEntity : class
{
    /// <summary>Composite-key delimiter (ASCII unit separator). Unlikely in real entity values; collisions in dashboard tile data are negligible.</summary>
    private const char KeyDelimiter = '';

    /// <summary>Sentinel surfaced on the wire for null property values.</summary>
    private const string NullKey = "(null)";

    private readonly IQueryableSource<TEntity> _source = source;
    private readonly IQueryEngine<TEntity> _engine = engine;
    private readonly QueryDefinition<TEntity> _definition = definition;

    public string Name { get; } = name;

    public async Task<PivotRunnerResult> ExecuteAsync(
        IReadOnlyList<string> rowFields,
        IReadOnlyList<string> columnFields,
        string? valueField,
        AggregateFunction aggregation,
        IReadOnlyDictionary<string, string>? dashboardFilters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rowFields);
        ArgumentNullException.ThrowIfNull(columnFields);

        if (rowFields.Count == 0)
        {
            throw new ArgumentException(
                $"Pivot for query '{Name}' requires at least one RowField.",
                nameof(rowFields));
        }

        // Whitelist enforcement — every row, column, and value field must be
        // declared on the QueryDefinition (the same set the admin grid honours).
        // Without this gate, a dashboard author could pivot any aggregate by
        // an undeclared categorical column or aggregate undeclared numerics.
        PropertyInfo[] rowProps = ResolveWhitelistedProperties(rowFields, nameof(rowFields));
        PropertyInfo[] colProps = ResolveWhitelistedProperties(columnFields, nameof(columnFields));

        PropertyInfo? valueProp = null;
        Type? valueUnderlying = null;
        if (aggregation != AggregateFunction.Count)
        {
            if (string.IsNullOrWhiteSpace(valueField))
            {
                throw new ArgumentException(
                    $"Aggregation '{aggregation}' on pivot for query '{Name}' requires a ValueField — was null or empty.",
                    nameof(valueField));
            }

            (valueProp, _) = AnalyticsColumnWhitelist.ResolveProperty(
                _definition, Name, valueField, nameof(valueField));
            valueUnderlying = Nullable.GetUnderlyingType(valueProp.PropertyType) ?? valueProp.PropertyType;

            if (!IsSupportedNumericType(valueUnderlying))
            {
                throw new NotSupportedException(FormattableString.Invariant(
                    $"Aggregation '{aggregation}' on field '{valueProp.Name}' (type '{valueUnderlying.Name}') is not supported. Supported types: int, long, decimal, double."));
            }
        }

        // Same QueryRequest the chart and table runners use — dashboard filters
        // layer in the same way and the QueryEngine pipeline (multi-tenancy,
        // soft-delete) applies upstream of streaming.
        QueryRequest request = new()
        {
            Filter = DashboardFilterTranslator.ToQueryRequestFilter(dashboardFilters),
        };

        Dictionary<string, CellAccumulator> cells = new(StringComparer.Ordinal);

        await foreach (TEntity entity in _engine
            .ExecuteStreamAsync(_source.GetQueryable(), request, cancellationToken)
            .ConfigureAwait(false))
        {
            string[] rowKeys = ProjectKeys(rowProps, entity);
            string[] colKeys = ProjectKeys(colProps, entity);
            string compositeKey = BuildCompositeKey(rowKeys, colKeys);

            if (!cells.TryGetValue(compositeKey, out CellAccumulator? acc))
            {
                acc = new CellAccumulator(rowKeys, colKeys);
                cells[compositeKey] = acc;
            }

            if (valueProp is not null)
            {
                object? rawValue = valueProp.GetValue(entity);
                if (rawValue is not null)
                {
                    acc.AddValue(Convert.ToDecimal(rawValue, CultureInfo.InvariantCulture));
                }
            }
            else
            {
                // Count path — no value field, just bump the counter.
                acc.IncrementCount();
            }
        }

        IReadOnlyList<PivotRunnerCell> result = [..
            cells.Values.Select(acc => new PivotRunnerCell(
                RowKeys: acc.RowKeys,
                ColumnKeys: acc.ColumnKeys,
                Value: acc.Compute(aggregation)))];

        // All cells aggregate the same value field — share its declared
        // currency code. Count never carries a currency (no value field).
        string? currencyCode = valueProp is not null
            ? ResolveCurrencyCode(valueProp)
            : null;

        return new PivotRunnerResult(result, currencyCode);
    }

    private string? ResolveCurrencyCode(PropertyInfo prop)
    {
        IReadOnlyList<ColumnDescriptor> columns = _definition.GetColumns();
        ColumnDescriptor? match = columns.FirstOrDefault(c =>
            string.Equals(c.PropertyName, prop.Name, StringComparison.Ordinal));
        return match?.CurrencyCode;
    }

    private PropertyInfo[] ResolveWhitelistedProperties(IReadOnlyList<string> fieldNames, string paramName)
    {
        var resolved = new PropertyInfo[fieldNames.Count];
        for (int i = 0; i < fieldNames.Count; i++)
        {
            (resolved[i], _) = AnalyticsColumnWhitelist.ResolveProperty(
                _definition, Name, fieldNames[i], paramName);
        }
        return resolved;
    }

    private static bool IsSupportedNumericType(Type underlying) =>
        underlying == typeof(int) || underlying == typeof(long)
        || underlying == typeof(decimal) || underlying == typeof(double);

    private static string[] ProjectKeys(PropertyInfo[] props, TEntity entity)
    {
        if (props.Length == 0)
        {
            return [];
        }

        string[] keys = new string[props.Length];
        for (int i = 0; i < props.Length; i++)
        {
            object? raw = props[i].GetValue(entity);
            keys[i] = raw is null
                ? NullKey
                : Convert.ToString(raw, CultureInfo.InvariantCulture) ?? NullKey;
        }
        return keys;
    }

    private static string BuildCompositeKey(string[] rowKeys, string[] colKeys)
    {
        // Two-section composite — row-section then column-section, separated
        // by a sentinel. ASCII unit separator () is unlikely in real
        // values; same delimiter strategy as Granit's other internal keys.
        return string.Concat(
            string.Join(KeyDelimiter, rowKeys),
            "",   // record separator between row and column sections
            string.Join(KeyDelimiter, colKeys));
    }

    private sealed class CellAccumulator(string[] rowKeys, string[] colKeys)
    {
        private readonly List<decimal> _values = [];
        private int _count;

        public string[] RowKeys { get; } = rowKeys;
        public string[] ColumnKeys { get; } = colKeys;

        public void AddValue(decimal value) => _values.Add(value);
        public void IncrementCount() => _count++;

        public decimal? Compute(AggregateFunction aggregation) =>
            aggregation switch
            {
                AggregateFunction.Count => _count,
                AggregateFunction.Sum => _values.Count == 0 ? 0m : _values.Sum(),
                AggregateFunction.Avg => _values.Count == 0 ? null : _values.Average(),
                AggregateFunction.Min => _values.Count == 0 ? null : _values.Min(),
                AggregateFunction.Max => _values.Count == 0 ? null : _values.Max(),
                _ => throw new NotSupportedException(
                    FormattableString.Invariant($"Aggregation '{aggregation}' is not supported.")),
            };
    }
}
