using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Metering.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering metering endpoints.
/// </summary>
public static class MeteringEndpointRouteBuilderExtensions
{
    /// <summary>Maps the metering endpoints.</summary>
    public static RouteGroupBuilder MapGranitMetering(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGranitGroup("metering")
            .WithTags("Metering");

        // Meter definition endpoints will be added in subsequent stories
        // Usage reporting endpoints will be added in subsequent stories
        // Quota status endpoints will be added in subsequent stories

        return group;
    }
}
