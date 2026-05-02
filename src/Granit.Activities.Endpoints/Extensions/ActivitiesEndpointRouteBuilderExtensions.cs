using Granit.Activities.Endpoints.Endpoints;
using Granit.Activities.Endpoints.Options;
using Granit.Activities.Endpoints.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Activities.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering activity endpoints.
/// </summary>
public static class ActivitiesEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the activities endpoints (list / get / create / complete / cancel /
    /// reassign / reschedule) onto the given route builder. The route group
    /// requires <c>Activities.Activities.Read</c>; per-endpoint write
    /// permissions (<c>Manage</c>, <c>Execute</c>) are layered on top.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize options.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitActivities(
        this IEndpointRouteBuilder endpoints,
        Action<ActivitiesEndpointsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        ActivitiesEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName)
            .RequireAuthorization(ActivitiesPermissions.Activities.Read);

        group.MapActivityEndpoints();
        group.MapActivityCalendarEndpoint();
        return group;
    }
}
