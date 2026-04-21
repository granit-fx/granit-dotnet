namespace Granit.Metering.Endpoints.Options;

/// <summary>
/// Configuration options for the metering endpoints.
/// </summary>
public sealed class MeteringEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "MeteringEndpoints";

    /// <summary>
    /// Route prefix for all metering endpoints.
    /// Default: <c>"metering"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "metering";

    /// <summary>
    /// OpenAPI tag name for metering endpoints.
    /// Default: <c>"Metering"</c>.
    /// </summary>
    public string TagName { get; set; } = "Metering";
}
