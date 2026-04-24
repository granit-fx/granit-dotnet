using Granit.Metering.Domain;
using Granit.Metering.Endpoints.Endpoints;
using Granit.Metering.Endpoints.Options;
using Granit.Metering.Endpoints.Permissions;
using Granit.QueryEngine.AspNetCore.Extensions;
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

        // Admin query endpoints — list / filter / sort / paginate / export via the QueryEngine.
        // GET /meters is now QueryEngine-backed: it returns a paged envelope (replacing the
        // previous flat array of Published meters) and supports filtering on every column
        // declared by MeterDefinitionQueryDefinition, including LifecycleStatus. URL-level
        // collisions are avoided because the QueryEngine routes (`/`, `/meta`, `/saved-views`)
        // do not overlap with the GUID-constrained CRUD routes (`/{id:guid}`, `/{id:guid}/...`)
        // mapped above.
        group.MapGranitGroup("meters")
            .MapGranitQuery<MeterDefinition>()
            .RequireAuthorization(MeteringPermissions.Meters.Read);

        group.MapGranitGroup("usage-aggregates")
            .MapGranitQuery<UsageAggregate>()
            .RequireAuthorization(MeteringPermissions.Usage.Read);

        return group;
    }
}
