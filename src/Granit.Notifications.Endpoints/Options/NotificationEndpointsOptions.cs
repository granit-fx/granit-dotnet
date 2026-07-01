namespace Granit.Notifications.Endpoints.Options;

/// <summary>
/// Configuration options for the Granit.Notifications HTTP endpoints.
/// </summary>
public sealed class NotificationEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Notifications:Endpoints";

    /// <summary>
    /// Route prefix for all notification endpoints.
    /// Default: <c>"notifications"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "notifications";

    /// <summary>
    /// OpenAPI tag name for all notification endpoints.
    /// Default: <c>"Notifications"</c>.
    /// </summary>
    public string TagName { get; set; } = "Notifications";
}
