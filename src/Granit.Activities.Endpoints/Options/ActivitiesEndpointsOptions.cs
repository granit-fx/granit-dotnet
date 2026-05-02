namespace Granit.Activities.Endpoints.Options;

/// <summary>
/// Configuration options for the activities endpoints.
/// </summary>
public sealed class ActivitiesEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "ActivitiesEndpoints";

    /// <summary>Route prefix for all activity endpoints. Default: <c>"activities"</c>.</summary>
    public string RoutePrefix { get; set; } = "activities";

    /// <summary>OpenAPI tag name for grouping endpoints in Scalar / Swagger UI. Default: <c>"Activities"</c>.</summary>
    public string TagName { get; set; } = "Activities";

    /// <summary>Maximum page size accepted by the list endpoint. Default: <c>100</c>.</summary>
    public int MaxPageSize { get; set; } = 100;

    /// <summary>Default page size when the caller does not specify one. Default: <c>20</c>.</summary>
    public int DefaultPageSize { get; set; } = 20;
}
