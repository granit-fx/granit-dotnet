namespace Granit.Scheduling.Endpoints.Options;

/// <summary>
/// Configuration options for the scheduled actions administration endpoints.
/// </summary>
public sealed class SchedulingEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Scheduling:Endpoints";

    /// <summary>
    /// Route prefix for all scheduling endpoints.
    /// Default: <c>"scheduling"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "scheduling";

    /// <summary>
    /// OpenAPI tag name for grouping scheduling endpoints.
    /// Default: <c>"Scheduling"</c>.
    /// </summary>
    public string TagName { get; set; } = "Scheduling";
}
