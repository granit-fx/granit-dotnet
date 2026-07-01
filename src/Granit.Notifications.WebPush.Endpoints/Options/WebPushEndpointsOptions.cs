namespace Granit.Notifications.WebPush.Endpoints.Options;

/// <summary>
/// Configuration options for the browser Web Push subscription HTTP endpoints.
/// </summary>
public sealed class WebPushEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Notifications:WebPush:Endpoints";

    /// <summary>
    /// Route prefix for the Web Push subscription endpoints. Default: <c>"notifications"</c> — so the
    /// routes resolve under <c>/notifications/push/subscriptions</c>, alongside the core notification
    /// endpoints.
    /// </summary>
    public string RoutePrefix { get; set; } = "notifications";

    /// <summary>
    /// OpenAPI tag name for the Web Push subscription endpoints.
    /// Default: <c>"Notifications - Web Push"</c>.
    /// </summary>
    public string TagName { get; set; } = "Notifications - Web Push";
}
