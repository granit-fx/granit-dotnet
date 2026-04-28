namespace Granit.Dashboards.Endpoints.Dtos;

/// <summary>
/// Payload for <c>PUT /dashboards/{id}/widgets/{widgetId}</c> — full
/// replacement of the widget's editable fields (layout + title + config).
/// WidgetType, MetricName, QueryName and RequiredPermission are intentionally
/// out of scope: switching widget kind or rebinding to a different
/// metric/query is delete + add, not edit.
/// </summary>
public sealed record UpdateWidgetRequest(
    int Position,
    int Width,
    int Height,
    string TitleLocalizationKey,
    string ConfigJson);
