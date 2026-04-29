using System.Text.Json;

namespace Granit.Analytics.Internal;

/// <summary>
/// Non-generic façade over a typed <c>QueryDefinition&lt;TEntity&gt;</c> that
/// materialises the first <c>pageSize</c> rows for a Table widget. One runner
/// is registered per query definition by
/// <c>AddGranitAnalyticsRunners</c> at startup, so the dashboard
/// render path resolves it by query name without reflection.
/// </summary>
internal interface ITableRunner
{
    /// <summary>The query definition's unique name.</summary>
    string Name { get; }

    /// <summary>
    /// Loads the first <paramref name="pageSize"/> rows through the QueryEngine
    /// pipeline (filters, default sort, multi-tenancy already applied via
    /// <c>BuildFilteredQuery</c> upstream) and projects each row to a flat
    /// JSON object containing only <paramref name="visibleColumns"/>.
    /// </summary>
    /// <param name="visibleColumns">Subset of column property names to surface; <see langword="null"/> = every visible column declared on the <c>QueryDefinition</c>.</param>
    /// <param name="pageSize">Maximum rows to return. Clamped against the QueryDefinition's <c>MaxPageSize</c>.</param>
    /// <param name="dashboardFilters">Dashboard-level filter spec layered on top of the QueryDefinition's filter pipeline — see <see cref="DashboardFilterTranslator"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">A column name in <paramref name="visibleColumns"/> is not declared by the <c>QueryDefinition</c>.</exception>
    Task<TableRunnerResult> ExecuteAsync(
        IReadOnlyList<string>? visibleColumns,
        int pageSize,
        IReadOnlyDictionary<string, string>? dashboardFilters,
        CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of an <see cref="ITableRunner.ExecuteAsync"/> call. Carries the
/// per-column metadata the wire envelope needs (<see cref="Columns"/>) plus
/// the projected row payload (<see cref="Rows"/>) and the count of matching
/// rows in the underlying source (<see cref="TotalRowCount"/>) so the
/// frontend can render "showing 5 of 142" affordances without re-querying.
/// </summary>
/// <param name="Columns">Column metadata: property name + localization key for the header. Order matches the order rows expose.</param>
/// <param name="Rows">Per-row JSON object — keys are camelCase property names, values are typed JSON primitives.</param>
/// <param name="TotalRowCount">Total matching rows in the underlying source — surfaces a "of N" affordance when more rows exist beyond <paramref name="Rows"/>'s page.</param>
internal sealed record TableRunnerResult(
    IReadOnlyList<TableRunnerColumn> Columns,
    IReadOnlyList<JsonElement> Rows,
    int TotalRowCount);

/// <summary>Column metadata surfaced on the wire envelope.</summary>
/// <param name="Name">Column property name (camelCase to match the row payload keys).</param>
/// <param name="LabelLocalizationKey">Localization key for the header, or <see langword="null"/> when the source <c>QueryDefinition</c> declared no localized label.</param>
/// <param name="CurrencyCode">ISO 4217 alpha-3 code from <c>ColumnBuilder.Currency(...)</c>; <see langword="null"/> for non-monetary columns. Drives per-cell currency formatting on the frontend.</param>
internal sealed record TableRunnerColumn(string Name, string? LabelLocalizationKey, string? CurrencyCode = null);
