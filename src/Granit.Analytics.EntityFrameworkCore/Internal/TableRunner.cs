using System.Reflection;
using System.Text.Json;
using Granit.Analytics.Internal;
using Granit.QueryEngine;

namespace Granit.Analytics.EntityFrameworkCore.Internal;

/// <summary>
/// Typed implementation of <see cref="ITableRunner"/> — closes over
/// <typeparamref name="TEntity"/> so the dashboard render path materialises
/// rows without reflection at request time, then projects each entity to a
/// camelCase JSON object containing only the requested columns.
/// </summary>
/// <remarks>
/// <para>
/// Reuses the <see cref="IQueryEngine{TEntity}"/> pipeline so the table tile
/// honours the same filter / sort / multi-tenancy contract as the admin grid
/// — a "5 most recent invoices" tile shows the same five invoices the grid
/// would show with the same default sort.
/// </para>
/// <para>
/// The QueryEngine clamps <c>pageSize</c> against the QueryDefinition's
/// <c>MaxPageSize</c>, so a misconfigured tile asking for 10 000 rows is
/// quietly capped — no DOS vector.
/// </para>
/// </remarks>
internal sealed class TableRunner<TEntity>(
    string name,
    IQueryableSource<TEntity> source,
    IQueryEngine<TEntity> engine,
    QueryDefinition<TEntity> definition) : ITableRunner
    where TEntity : class
{
    // Shared serialisation options: camelCase property names match the wire
    // convention used by the rest of the dashboard render envelope (ADR-039
    // §6.1). Every row passes through this so column keys stay consistent.
    private static readonly JsonSerializerOptions RowJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IQueryableSource<TEntity> _source = source;
    private readonly IQueryEngine<TEntity> _engine = engine;
    private readonly QueryDefinition<TEntity> _definition = definition;

    public string Name { get; } = name;

    public async Task<TableRunnerResult> ExecuteAsync(
        IReadOnlyList<string>? visibleColumns,
        int pageSize,
        IReadOnlyDictionary<string, string>? dashboardFilters,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ColumnDescriptor> selectedColumns = ResolveColumns(visibleColumns);

        QueryRequest request = new()
        {
            Page = 1,
            PageSize = pageSize,
            Filter = DashboardFilterTranslator.ToQueryRequestFilter(dashboardFilters),
        };

        PagedResult<TEntity> page = await _engine
            .ExecuteAsync(_source.GetQueryable(), request, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<JsonElement> rows = ProjectRows(page.Items, selectedColumns);
        IReadOnlyList<TableRunnerColumn> columns = [..
            selectedColumns.Select(c => new TableRunnerColumn(
                Name: ToCamelCase(c.PropertyName),
                LabelLocalizationKey: c.LabelKey,
                CurrencyCode: c.CurrencyCode))];

        return new TableRunnerResult(columns, rows, page.TotalCount ?? page.Items.Count);
    }

    private List<ColumnDescriptor> ResolveColumns(IReadOnlyList<string>? visibleColumns)
    {
        IReadOnlyList<ColumnDescriptor> declared = _definition.GetColumns();

        if (visibleColumns is null || visibleColumns.Count == 0)
        {
            // Default — every visible column the QueryDefinition declared,
            // ordered by the declared display order.
            return [.. declared.Where(c => c.IsVisible).OrderBy(c => c.Order)];
        }

        var byName = declared.ToDictionary(c => c.PropertyName, StringComparer.OrdinalIgnoreCase);

        var resolved = new List<ColumnDescriptor>(visibleColumns.Count);
        foreach (string requested in visibleColumns)
        {
            if (!byName.TryGetValue(requested, out ColumnDescriptor? col))
            {
                throw new ArgumentException(
                    $"Visible column '{requested}' is not declared by query definition '{Name}'.",
                    nameof(visibleColumns));
            }

            resolved.Add(col);
        }

        return resolved;
    }

    private static List<JsonElement> ProjectRows(
        IReadOnlyList<TEntity> entities,
        IReadOnlyList<ColumnDescriptor> columns)
    {
        // Cache the PropertyInfo lookup once per call — the entity type is
        // closed at construction so the lookup cost is per-render, not per-row.
        // Shadow properties are skipped: they require EF.Property<T>(entity, name)
        // which the runner does not reach for in v1.
        var propByColumn = new Dictionary<string, PropertyInfo?>(StringComparer.Ordinal);
        foreach (ColumnDescriptor col in columns)
        {
            propByColumn[col.PropertyName] = col.IsShadowProperty
                ? null
                : typeof(TEntity).GetProperty(col.PropertyName, BindingFlags.Public | BindingFlags.Instance);
        }

        var rows = new List<JsonElement>(entities.Count);
        foreach (TEntity entity in entities)
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (ColumnDescriptor col in columns)
            {
                PropertyInfo? prop = propByColumn[col.PropertyName];
                row[col.PropertyName] = prop?.GetValue(entity);
            }

            rows.Add(JsonSerializer.SerializeToElement(row, RowJsonOptions));
        }

        return rows;
    }

    private static string ToCamelCase(string value) =>
        string.IsNullOrEmpty(value)
            ? value
            : JsonNamingPolicy.CamelCase.ConvertName(value);
}
