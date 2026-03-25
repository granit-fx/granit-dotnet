namespace Granit.BackgroundJobs.Endpoints.Options;

/// <summary>
/// Configuration options for the background jobs administration endpoints.
/// Bind from <c>"BackgroundJobsEndpoints"</c> or pass an action to
/// <see cref="Extensions.BackgroundJobsEndpointRouteBuilderExtensions.MapBackgroundJobsEndpoints"/>.
/// </summary>
public sealed class BackgroundJobsEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "BackgroundJobsEndpoints";

    /// <summary>
    /// Route prefix for all background jobs endpoints.
    /// Default: <c>"background-jobs"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "background-jobs";

    /// <summary>
    /// OpenAPI tag name for grouping background jobs endpoints in Swagger UI.
    /// Default: <c>"Background Jobs"</c>.
    /// </summary>
    public string TagName { get; set; } = "Background Jobs";
}
