namespace Granit.Hostnames.Endpoints.Options;

/// <summary>
/// Options for custom hostname management endpoints.
/// </summary>
public sealed class HostnamesEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Hostnames:Endpoints";

    /// <summary>
    /// Route prefix for all hostname endpoints.
    /// Default: <c>"hostnames"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "hostnames";

    /// <summary>
    /// OpenAPI tag name for all hostname endpoints.
    /// Default: <c>"Hostnames"</c>.
    /// </summary>
    public string TagName { get; set; } = "Hostnames";
}
