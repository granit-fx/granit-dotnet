using Granit.Metering.Endpoints.Endpoints;
using Granit.Metering.Endpoints.Options;
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
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="MeteringEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitMetering(
        this IEndpointRouteBuilder endpoints,
        Action<MeteringEndpointsOptions>? configure = null)
    {
        MeteringEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        group.MapMeterDefinitionEndpoints();
        group.MapUsageEndpoints();

        return group;
    }
}
