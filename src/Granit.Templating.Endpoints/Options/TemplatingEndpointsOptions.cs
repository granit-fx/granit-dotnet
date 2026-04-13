namespace Granit.Templating.Endpoints.Options;

/// <summary>
/// Configuration options for the Granit templating admin HTTP endpoints.
/// </summary>
public sealed class TemplatingEndpointsOptions
{
    /// <summary>
    /// Route prefix for all template admin endpoints.
    /// Default: <c>"templating"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "templating";

    /// <summary>
    /// OpenAPI tag name for all template admin endpoints.
    /// Default: <c>"Templates"</c>.
    /// </summary>
    public string TagName { get; set; } = "Templates";
}
