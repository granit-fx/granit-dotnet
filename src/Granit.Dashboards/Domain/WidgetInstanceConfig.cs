namespace Granit.Dashboards.Domain;

/// <summary>
/// Per-instance overrides applied on top of a <see cref="WidgetInstance"/>'s default
/// rendering. P3.2 of the dashboards-architecture-proposals roadmap — separates the
/// "what was imported from the definition" config (carried inline on the aggregate)
/// from the "what the admin tweaked at runtime" overrides.
/// </summary>
/// <param name="TitleLocalizationKeyOverride">
/// Replacement localization key for the widget title. <c>null</c> = honour the
/// imported <see cref="WidgetInstance.TitleLocalizationKey"/>.
/// </param>
/// <param name="ColorOverride">
/// Hex colour the frontend applies to the widget's primary visual (KPI value, chart
/// accent, ...). <c>null</c> = use the active theme palette.
/// </param>
/// <param name="UnitOverride">
/// Unit suffix (e.g. <c>"€"</c>, <c>"kWh"</c>, <c>"°C"</c>). When prefixed with
/// <c>Unit:</c>, resolved via i18n. <c>null</c> = the data source's declared unit
/// (or none).
/// </param>
/// <param name="DecimalsOverride">
/// Number of decimal places for numeric formatting. <c>null</c> = the data source's
/// default (typically 2 for currency, 0 for counts).
/// </param>
/// <param name="Thresholds">
/// Optional value thresholds applied for conditional colouring (e.g. a KPI turns red
/// when below a target). Evaluated left-to-right; the first matching threshold's
/// colour wins. <c>null</c> or empty = no threshold-based colouring.
/// </param>
/// <remarks>
/// Persisted as a JSON column on the <see cref="WidgetInstance"/> row so the schema
/// stays additive when new override fields ship — same rationale as
/// <see cref="WidgetInstance.ConfigJson"/>.
/// </remarks>
public sealed record WidgetInstanceConfig(
    string? TitleLocalizationKeyOverride = null,
    string? ColorOverride = null,
    string? UnitOverride = null,
    int? DecimalsOverride = null,
    IReadOnlyList<WidgetThreshold>? Thresholds = null);

/// <summary>
/// Threshold rule for conditional colouring of a widget's primary value.
/// </summary>
/// <param name="Value">The numeric threshold the data point is compared against.</param>
/// <param name="Color">Hex colour applied when the rule matches.</param>
/// <param name="Operator">Comparison operator — see <see cref="WidgetThresholdOperator"/>.</param>
public sealed record WidgetThreshold(
    decimal Value,
    string Color,
    WidgetThresholdOperator Operator);

/// <summary>Comparison operator used by <see cref="WidgetThreshold"/>.</summary>
public enum WidgetThresholdOperator
{
    /// <summary>Match when the data point is greater than or equal to the threshold.</summary>
    GreaterThanOrEqual = 0,

    /// <summary>Match when the data point is less than or equal to the threshold.</summary>
    LessThanOrEqual = 1,

    /// <summary>Match when the data point equals the threshold (within reasonable tolerance).</summary>
    Equal = 2,
}
