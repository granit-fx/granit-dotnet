using Granit.Analytics.Dashboards.Widgets;

namespace Granit.Analytics.Endpoints.Dtos.Widgets;

/// <summary>
/// Body of <c>POST /widgets/kpi/render</c> — a typed
/// <see cref="KpiWidgetDefinition"/> (with its embedded <c>Datasource</c>) plus
/// an optional render context. Lets the frontend's edition / preview /
/// catalogue paths fetch a single KPI snapshot without persisting the widget.
/// </summary>
/// <param name="Definition">The widget definition to render — same shape ADRs land at module-time.</param>
/// <param name="Context">Optional per-render context (period, locale, filters).</param>
public sealed record KpiWidgetRenderRequest(
    KpiWidgetDefinition Definition,
    WidgetRenderContextRequest? Context = null);

/// <summary>Body of <c>POST /widgets/chart/render</c>.</summary>
/// <param name="Definition">Typed chart widget definition.</param>
/// <param name="Context">Optional per-render context.</param>
public sealed record ChartWidgetRenderRequest(
    ChartWidgetDefinition Definition,
    WidgetRenderContextRequest? Context = null);

/// <summary>Body of <c>POST /widgets/table/render</c>.</summary>
/// <param name="Definition">Typed table widget definition.</param>
/// <param name="Context">Optional per-render context.</param>
public sealed record TableWidgetRenderRequest(
    TableWidgetDefinition Definition,
    WidgetRenderContextRequest? Context = null);

/// <summary>Body of <c>POST /widgets/pivot/render</c>.</summary>
/// <param name="Definition">Typed pivot widget definition.</param>
/// <param name="Context">Optional per-render context.</param>
public sealed record PivotWidgetRenderRequest(
    PivotWidgetDefinition Definition,
    WidgetRenderContextRequest? Context = null);

/// <summary>Body of <c>POST /widgets/map/render</c>.</summary>
/// <param name="Definition">Typed map widget definition.</param>
/// <param name="Context">Optional per-render context.</param>
public sealed record MapWidgetRenderRequest(
    MapWidgetDefinition Definition,
    WidgetRenderContextRequest? Context = null);
