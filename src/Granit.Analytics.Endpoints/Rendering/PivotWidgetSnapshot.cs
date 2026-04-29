using Granit.QueryEngine.Filtering;

namespace Granit.Analytics.Endpoints.Rendering;

/// <summary>
/// Wire-shape snapshot for the <c>"Pivot"</c> widget kind. Echoes the
/// declarative configuration (<see cref="RowFields"/>, <see cref="ColumnFields"/>,
/// <see cref="ValueField"/>, <see cref="Aggregation"/>) so the frontend
/// renders without re-reading the widget config, and ships one
/// <see cref="PivotCell"/> per (row-tuple × column-tuple) bucket as a flat
/// list — the frontend pivots into a matrix client-side.
/// </summary>
/// <param name="RowFields">Row-axis field names, in the order they were declared on the widget.</param>
/// <param name="ColumnFields">Column-axis field names, in declared order. Empty when the widget has only row dimensions.</param>
/// <param name="ValueField">Aggregated field; <see langword="null"/> for <see cref="AggregateFunction.Count"/>.</param>
/// <param name="Aggregation">Aggregation applied per cell — drives the value-axis label client-side.</param>
/// <param name="Cells">Per-cell results. Order is preserved as the underlying stream produces them; the frontend pivots into a row-major matrix.</param>
/// <param name="Currency">ISO 4217 alpha-3 code from the value field's <c>ColumnBuilder.Currency(...)</c> declaration; <see langword="null"/> when the field is not monetary or for <c>Count</c>. All cells share this currency since they aggregate the same value field.</param>
public sealed record PivotWidgetSnapshot(
    IReadOnlyList<string> RowFields,
    IReadOnlyList<string> ColumnFields,
    string? ValueField,
    AggregateFunction Aggregation,
    IReadOnlyList<PivotCell> Cells,
    string? Currency = null);

/// <summary>One cell of the pivot matrix.</summary>
/// <param name="RowKeys">Row-axis key tuple matching <see cref="PivotWidgetSnapshot.RowFields"/>. Null property values surface as <c>"(null)"</c>.</param>
/// <param name="ColumnKeys">Column-axis key tuple matching <see cref="PivotWidgetSnapshot.ColumnFields"/>. Empty when no column fields were requested.</param>
/// <param name="Value">
/// Aggregate value for the cell. <see langword="null"/> when the aggregation
/// is <c>Avg</c>/<c>Min</c>/<c>Max</c> over a cell with no usable values —
/// the frontend renders "—" for that data point. <c>Count</c> and <c>Sum</c>
/// always carry a non-null value (zero for empty cells).
/// </param>
public sealed record PivotCell(
    IReadOnlyList<string> RowKeys,
    IReadOnlyList<string> ColumnKeys,
    decimal? Value);
