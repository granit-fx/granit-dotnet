using Granit.Presence.Endpoints.Endpoints;
using Granit.Presence.Endpoints.Options;
using Granit.Presence.Endpoints.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Presence.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering the presence endpoints onto a route builder.
/// </summary>
public static class PresenceEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the Granit presence endpoints. Requires the caller to be authenticated.
    /// The self-management routes require <c>Presence.Self.Manage</c>; the query routes
    /// require <c>Presence.Users.Read</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="PresenceEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitPresence(
        this IEndpointRouteBuilder endpoints,
        Action<PresenceEndpointsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        PresenceEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName)
            .RequireAuthorization();

        group.MapSelfEndpoints()
            .RequireAuthorization(PresencePermissions.Self.Manage);

        group.MapQueryEndpoints()
            .RequireAuthorization(PresencePermissions.Users.Read);

        return group;
    }
}
