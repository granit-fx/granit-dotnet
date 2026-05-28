namespace Granit.Presence.Endpoints.Options;

/// <summary>
/// Configuration options for the presence HTTP endpoints.
/// </summary>
public sealed class PresenceEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Presence:Endpoints";

    /// <summary>Route prefix for the presence endpoints. Default: <c>"presence"</c>.</summary>
    public string RoutePrefix { get; set; } = "presence";

    /// <summary>OpenAPI tag name for grouping presence endpoints. Default: <c>"Presence"</c>.</summary>
    public string TagName { get; set; } = "Presence";

    /// <summary>OpenAPI tag name for the resource-room endpoints. Default: <c>"Presence - Rooms"</c>.</summary>
    public string RoomsTagName { get; set; } = "Presence - Rooms";
}
