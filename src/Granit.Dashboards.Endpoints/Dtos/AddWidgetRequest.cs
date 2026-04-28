namespace Granit.Dashboards.Endpoints.Dtos;

/// <summary>
/// Payload for <c>POST /dashboards/{id}/widgets</c> — pins a new widget into
/// the dashboard's widget pool. The server allocates the widget id (callers
/// don't choose it). Optional fields (MetricName, QueryName, RequiredPermission)
/// follow the widget kind: KPI widgets carry MetricName, table widgets carry
/// QueryName, free-form widgets carry neither.
/// </summary>
public sealed record AddWidgetRequest(
    string WidgetType,
    int Position,
    int Width,
    int Height,
    string TitleLocalizationKey,
    string ConfigJson,
    string? MetricName = null,
    string? QueryName = null,
    string? RequiredPermission = null);
