namespace Granit.Dashboards;

/// <summary>
/// Per-series presentation hints attached to a <see cref="Datasource"/> rather
/// than to the consuming widget. P2.2 of the dashboards-architecture-proposals
/// roadmap. The same chart widget can be reused with different data shapes —
/// a "Revenue by Region" chart and a "Revenue by Product" chart are the same
/// <c>ChartWidgetDefinition</c> with different datasources — so colours and
/// units belong to the data binding, not the visual.
/// </summary>
/// <param name="Key">
/// Series identifier — matches the group-by value (chart) or the field name
/// (multi-series KPI / pivot).
/// </param>
/// <param name="LabelLocalizationKey">
/// Localization key for the display label (legend, tooltip). <c>null</c> = the
/// frontend falls back to <see cref="Key"/>.
/// </param>
/// <param name="Color">
/// Hex colour override. <c>null</c> = the active theme palette by series index.
/// </param>
/// <param name="Unit">
/// Unit suffix (e.g. <c>"€"</c>, <c>"kWh"</c>, <c>"°C"</c>). When prefixed with
/// <c>Unit:</c>, resolved via i18n. <c>null</c> = no unit suffix.
/// </param>
/// <param name="Decimals">
/// Decimal places for numeric formatting. <c>null</c> = the widget's default
/// (typically 0 for counts, 2 for currency).
/// </param>
public sealed record DataKeyFormat(
    string Key,
    string? LabelLocalizationKey = null,
    string? Color = null,
    string? Unit = null,
    int? Decimals = null);
