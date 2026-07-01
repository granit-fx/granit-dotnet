namespace Granit.Notifications.MobilePush.Endpoints.Options;

/// <summary>
/// Configuration options for the mobile push device token HTTP endpoints.
/// </summary>
public sealed class MobilePushEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Notifications:MobilePush:Endpoints";

    /// <summary>
    /// Route prefix for the mobile push token endpoints.
    /// Default: <c>"api/notifications/mobile-push/tokens"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "api/notifications/mobile-push/tokens";

    /// <summary>
    /// OpenAPI tag name for the mobile push token endpoints.
    /// Default: <c>"Notifications - Mobile Push"</c>.
    /// </summary>
    public string TagName { get; set; } = "Notifications - Mobile Push";
}
