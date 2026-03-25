namespace Granit.Timeline.Endpoints.Options;

/// <summary>
/// Configuration options for the timeline endpoints.
/// </summary>
public sealed class TimelineEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "TimelineEndpoints";

    /// <summary>
    /// Route prefix for all timeline endpoints.
    /// Default: <c>"timeline"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "timeline";

    /// <summary>
    /// OpenAPI tag name for grouping timeline endpoints in Swagger UI.
    /// Default: <c>"Timeline"</c>.
    /// </summary>
    public string TagName { get; set; } = "Timeline";
}
