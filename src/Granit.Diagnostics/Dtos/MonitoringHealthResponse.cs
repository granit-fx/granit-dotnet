namespace Granit.Diagnostics.Dtos;

/// <summary>
/// Aggregated health status of all registered health checks.
/// </summary>
/// <param name="Services">Individual service health entries.</param>
/// <param name="CheckedAt">Timestamp when the health checks were executed.</param>
public sealed record MonitoringHealthResponse(
    IReadOnlyList<ServiceHealthResponse> Services,
    DateTimeOffset CheckedAt);

/// <summary>
/// Health status of a single registered health check.
/// </summary>
/// <param name="Id">Health check registration name (e.g. <c>"postgresql"</c>, <c>"keycloak"</c>).</param>
/// <param name="Name">Display name derived from the registration name.</param>
/// <param name="Status">One of <c>"healthy"</c>, <c>"degraded"</c>, or <c>"down"</c>.</param>
/// <param name="ResponseTimeMs">Execution duration in milliseconds, rounded to one decimal place.</param>
/// <param name="Description">Optional description from the health check registration.</param>
/// <param name="Tags">Tags from the health check registration (e.g. <c>"readiness"</c>, <c>"startup"</c>).</param>
public sealed record ServiceHealthResponse(
    string Id,
    string Name,
    string Status,
    double? ResponseTimeMs,
    string? Description,
    IReadOnlyList<string> Tags);
