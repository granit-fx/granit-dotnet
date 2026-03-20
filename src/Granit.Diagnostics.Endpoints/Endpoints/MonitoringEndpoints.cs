using Granit.Diagnostics.Abstractions;
using Granit.Diagnostics.Dtos;
using Granit.Diagnostics.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Diagnostics.Endpoints.Endpoints;

/// <summary>
/// Monitoring endpoint that returns aggregated health status of all registered services.
/// </summary>
internal static class MonitoringEndpoints
{
    internal static void Map(RouteGroupBuilder group)
    {
        group.MapGet("health", HandleGetHealthAsync)
            .RequireAuthorization(DiagnosticsPermissions.Monitoring.Read)
            .WithName("GetMonitoringHealth")
            .WithSummary("Aggregated health status of all registered services")
            .WithDescription("Returns the current health status, response time, and description for every registered health check. Results are cached for the configured monitoring cache duration (default 30 seconds).")
            .Produces<MonitoringHealthResponse>();
    }

    private static async Task<Ok<MonitoringHealthResponse>> HandleGetHealthAsync(
        [FromServices] IHealthCheckAggregator aggregator,
        CancellationToken cancellationToken)
    {
        MonitoringHealthResponse result = await aggregator
            .CheckAllAsync(cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(result);
    }
}
