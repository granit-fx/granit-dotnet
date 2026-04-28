namespace Granit.Dashboards.Endpoints.Dtos;

/// <summary>
/// Wire-level projection of a persisted <c>WidgetInstance</c>. Carries the
/// type discriminator + denormalised metric / query references for client-side
/// validation, plus the kind-specific JSON config blob the frontend interprets
/// against the <c>WidgetType</c>.
/// </summary>
/// <param name="Id">Widget instance identifier.</param>
/// <param name="WidgetType">Kind discriminator (<c>Markdown</c>, <c>Image</c>, <c>Text</c>, <c>Kpi</c>, <c>Chart</c>, <c>Table</c>, <c>Pivot</c>, ...).</param>
/// <param name="Position">Dense-ranked grid order — 0-based.</param>
/// <param name="Width">Grid columns.</param>
/// <param name="Height">Grid rows.</param>
/// <param name="TitleLocalizationKey">Localization key for the widget title — typically <c>Widget:{DashboardName}.{Slug}</c>.</param>
/// <param name="MetricName">Denormalised metric reference for KPI widgets bound to a <c>MetricDatasource</c>; null otherwise.</param>
/// <param name="QueryName">Denormalised query reference for KPI widgets bound to a <c>QueryAggregateDatasource</c>, or for Chart / Table / Pivot widgets; null otherwise.</param>
/// <param name="ConfigJson">Kind-specific JSON payload — interpreted by the frontend against <see cref="WidgetType"/>. Includes the full <c>Datasource</c> for KPI widgets.</param>
/// <param name="RequiredPermission">Optional per-widget permission override.</param>
public sealed record WidgetInstanceResponse(
    Guid Id,
    string WidgetType,
    int Position,
    int Width,
    int Height,
    string TitleLocalizationKey,
    string? MetricName,
    string? QueryName,
    string ConfigJson,
    string? RequiredPermission);
