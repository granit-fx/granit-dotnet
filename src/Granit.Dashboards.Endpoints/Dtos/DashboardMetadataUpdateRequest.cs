namespace Granit.Dashboards.Endpoints.Dtos;

/// <summary>
/// Payload for <c>PUT /dashboards/{id}</c> — full replacement of the dashboard's
/// editable metadata (name + grid layout). Status, source-definition fields,
/// and the widget pool are immutable through this endpoint and are managed by
/// dedicated routes (state transitions, widget endpoints).
/// </summary>
public sealed record DashboardMetadataUpdateRequest(
    string Name,
    int LayoutColumns,
    int LayoutRowHeight);
