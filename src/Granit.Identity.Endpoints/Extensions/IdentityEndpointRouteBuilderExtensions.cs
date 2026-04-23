using Granit.Identity.Endpoints.Endpoints;
using Granit.Identity.Endpoints.Options;
using Granit.Identity.Endpoints.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering identity user cache endpoints.
/// </summary>
public static class IdentityEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the identity user cache endpoints onto the given route builder.
    /// </summary>
    /// <remarks>
    /// <para>Registers endpoints for:</para>
    /// <list type="bullet">
    /// <item>Search, get by ID, batch resolve (<c>Identity.Users.Read</c> permission)</item>
    /// <item>Sync, sync-all (<c>Identity.Users.Sync</c> permission)</item>
    /// <item>GDPR erase, pseudonymize (<c>Identity.Users.Delete</c> permission)</item>
    /// <item>Stats (<c>Identity.Users.Read</c> permission)</item>
    /// <item>Webhook (signature-validated, no user authentication required)</item>
    /// </list>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="IdentityEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitIdentityUserCache(
        this IEndpointRouteBuilder endpoints,
        Action<IdentityEndpointsOptions>? configure = null)
    {
        IdentityEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        // Capabilities endpoint
        group
            .RequireAuthorization(IdentityPermissions.Users.Read)
            .MapCapabilitiesEndpoints();

        // Read endpoints (list, get, batch)
        group
            .RequireAuthorization(IdentityPermissions.Users.Read)
            .MapReadEndpoints();

        // Stats endpoint
        group
            .RequireAuthorization(IdentityPermissions.Users.Read)
            .MapStatsEndpoints();

        // Sync endpoints
        group
            .RequireAuthorization(IdentityPermissions.Users.Sync)
            .MapSyncEndpoints();

        // GDPR endpoints
        group
            .RequireAuthorization(IdentityPermissions.Users.Delete)
            .MapGdprEndpoints();

        // Webhook endpoint (outside the authorized group — uses signature validation)
        endpoints.MapWebhookEndpoint("", options.WebhookTagName);

        return group;
    }
}
