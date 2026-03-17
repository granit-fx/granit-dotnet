namespace Granit.Features.Endpoints.Options;

/// <summary>
/// Options for feature management endpoints.
/// </summary>
public sealed class FeaturesEndpointsOptions
{
    /// <summary>
    /// Route prefix for all feature endpoints.
    /// Default: <c>"features"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "features";

    /// <summary>
    /// OpenAPI tag name for all feature endpoints.
    /// Default: <c>"Features"</c>.
    /// </summary>
    public string TagName { get; set; } = "Features";
}
