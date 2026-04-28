using Granit.Dashboards;

namespace Granit.Analytics.Dashboards.Widgets;

/// <summary>
/// A single-value KPI tile bound to a <c>MetricDefinition</c>. The most common widget
/// kind — typically renders the metric value, an optional period delta, and a
/// favorable / unfavorable color cue driven by <c>MetricDefinition.IsHigherBetter</c>.
/// </summary>
/// <param name="Slug">See <see cref="WidgetDefinition.Slug"/>.</param>
/// <param name="MetricName">The <c>MetricDefinition.Name</c> this KPI surfaces (e.g. <c>"Granit.Invoicing.UnpaidInvoiceCountMetric"</c>).</param>
/// <param name="Position">See <see cref="WidgetDefinition.Position"/>.</param>
/// <param name="Size">Defaults to <see cref="WidgetSize.SmallKpi"/>.</param>
/// <param name="RequiredPermission">See <see cref="WidgetDefinition.RequiredPermission"/>.</param>
/// <param name="TimeWindowOverride">See <see cref="WidgetDefinition.TimeWindowOverride"/>.</param>
/// <param name="Actions">See <see cref="WidgetDefinition.Actions"/>.</param>
public sealed record KpiWidgetDefinition(
    string Slug,
    string MetricName,
    int Position,
    WidgetSize? Size = null,
    string? RequiredPermission = null,
    DashboardTimeWindow? TimeWindowOverride = null,
    IReadOnlyList<WidgetAction>? Actions = null)
    : WidgetDefinition(Slug, Position, Size ?? WidgetSize.SmallKpi, RequiredPermission, TimeWindowOverride, Actions);
