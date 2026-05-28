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
    /// <remarks>
    /// <para>
    /// <b>CSRF.</b> The mutating endpoints (<c>PUT /my</c>, <c>DELETE /my/override</c>,
    /// <c>POST /my/poll</c>, <c>POST /users/batch</c>) are state-changing and must NOT be
    /// reachable directly from a browser session without an anti-forgery token. The intended
    /// topology mounts these endpoints behind <c>Granit.Bff</c>, which enforces the CSRF
    /// token on every mutating call. Hosting these endpoints standalone behind cookie auth
    /// requires the caller to add the equivalent CSRF middleware before <see cref="MapGranitPresence"/>.
    /// </para>
    /// <para>
    /// <b>Visibility.</b> Cross-user reads are filtered by <c>IPresenceVisibilityPolicy</c>.
    /// The default implementation is permissive and only suitable for single-tenant deployments;
    /// multi-tenant apps MUST register a tenant-aware replacement.
    /// </para>
    /// <para>
    /// <b>Rate limiting.</b> Each endpoint declares a Granit rate-limiting policy
    /// (<c>presence-poll</c>, <c>presence-mutate</c>, <c>presence-query</c>). Configure quotas
    /// under <c>RateLimiting:Policies</c> — see <c>PresenceRateLimitPolicies</c>.
    /// </para>
    /// </remarks>
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

        // Resource-scoped rooms live under the same prefix but carry their own OpenAPI tag.
        // Per-route RequireAuthorization is applied at the endpoint declaration so each verb
        // can require a different permission (Presence.Rooms.Read vs .Join).
        endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.RoomsTagName)
            .RequireAuthorization()
            .MapRoomEndpoints();

        return group;
    }
}
