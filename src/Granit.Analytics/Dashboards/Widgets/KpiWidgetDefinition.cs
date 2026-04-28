using Granit.Dashboards;

namespace Granit.Analytics.Dashboards.Widgets;

/// <summary>
/// A single-value KPI tile. The most common widget kind — typically renders the
/// value, an optional period delta, and a favorable / unfavorable color cue
/// driven by <c>MetricDefinition.IsHigherBetter</c> when bound via
/// <see cref="MetricDatasource"/>.
/// </summary>
/// <param name="Slug">See <see cref="WidgetDefinition.Slug"/>.</param>
/// <param name="Datasource">
/// Data binding for the KPI value — typically a <see cref="MetricDatasource"/>
/// (analytics dashboards), <see cref="QueryAggregateDatasource"/> (ad-hoc admin
/// pages reusing the same widget), or <see cref="TelemetryDatasource"/> (IoT
/// gauge). P2.2 of the dashboards-architecture-proposals roadmap — replaces the
/// previous <c>MetricName</c> string field. Use <c>Datasource.Metric(...)</c> /
/// <c>Datasource.QueryAggregate(...)</c> / <c>Datasource.Telemetry(...)</c>
/// factories for compact call sites.
/// </param>
/// <param name="Position">See <see cref="WidgetDefinition.Position"/>.</param>
/// <param name="Size">Defaults to <see cref="WidgetSize.SmallKpi"/>.</param>
/// <param name="RequiredPermission">See <see cref="WidgetDefinition.RequiredPermission"/>.</param>
/// <param name="TimeWindowOverride">See <see cref="WidgetDefinition.TimeWindowOverride"/>.</param>
/// <param name="Actions">See <see cref="WidgetDefinition.Actions"/>.</param>
public sealed record KpiWidgetDefinition(
    string Slug,
    Datasource Datasource,
    int Position,
    WidgetSize? Size = null,
    string? RequiredPermission = null,
    DashboardTimeWindow? TimeWindowOverride = null,
    IReadOnlyList<WidgetAction>? Actions = null)
    : WidgetDefinition(Slug, Position, Size ?? WidgetSize.SmallKpi, RequiredPermission, TimeWindowOverride, Actions);
