using System.Text.Json;

namespace Granit.Analytics.Endpoints.Rendering;

/// <summary>
/// Wire-shape snapshot for the <c>"Table"</c> widget kind. The dashboard
/// render endpoint inlines this as the <c>snapshot</c> field of the
/// per-widget envelope. The frontend's table renderer consumes it directly:
/// <see cref="Columns"/> drives the header order, <see cref="Rows"/> carries
/// the body, <see cref="TotalRowCount"/> drives the "showing N of M"
/// affordance.
/// </summary>
/// <param name="Columns">Column metadata in display order. Each entry's <see cref="TableWidgetColumn.Name"/> matches a key in every row payload.</param>
/// <param name="Rows">Per-row JSON object (camelCase keys, typed JSON primitives). Length is at most the widget's configured page size.</param>
/// <param name="TotalRowCount">Total rows in the underlying source after filters apply. May exceed <c>Rows.Count</c> when the page is full.</param>
public sealed record TableWidgetSnapshot(
    IReadOnlyList<TableWidgetColumn> Columns,
    IReadOnlyList<JsonElement> Rows,
    int TotalRowCount);

/// <summary>One column header on the wire envelope.</summary>
/// <param name="Name">Column property name (camelCase — matches the key in every row payload).</param>
/// <param name="LabelLocalizationKey">Localization key for the header. <see langword="null"/> when the source <c>QueryDefinition</c> declared no localized label — the frontend falls back to the property name.</param>
/// <param name="CurrencyCode">ISO 4217 alpha-3 currency code from the column's <c>ColumnBuilder.Currency(...)</c> declaration. When present the frontend formats the column's row values with the matching currency symbol + locale; <see langword="null"/> for non-monetary columns.</param>
public sealed record TableWidgetColumn(string Name, string? LabelLocalizationKey, string? CurrencyCode = null);
