using Granit.Activities.Endpoints.Endpoints;
using Granit.Activities.Endpoints.Options;
using Granit.Activities.Endpoints.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
    /// <remarks>
    /// Options are bound from the <c>ActivitiesEndpoints</c> configuration
    /// section by <see cref="GranitActivitiesEndpointsModule"/>. Hosts that need
    /// programmatic overrides should call
    /// <c>services.Configure&lt;ActivitiesEndpointsOptions&gt;(...)</c> in their
    /// composition root rather than mutating options here.
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitActivities(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        ActivitiesEndpointsOptions options = endpoints.ServiceProvider
            .GetRequiredService<IOptions<ActivitiesEndpointsOptions>>().Value;

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName)
            .RequireAuthorization(ActivitiesPermissions.Activities.Read);

        group.MapActivityEndpoints();
        group.MapActivityCalendarEndpoint();
        return group;
    }
}
