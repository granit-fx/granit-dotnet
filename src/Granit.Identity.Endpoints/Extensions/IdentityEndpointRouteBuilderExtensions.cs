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

        // Each permission scope gets its own RouteGroupBuilder. Reusing the same group
        // and stacking RequireAuthorization() accumulates policies (AND semantics), which
        // would force every endpoint to satisfy every previously-registered permission —
        // making sync and GDPR endpoints unreachable without the union of all permissions.
        RouteGroupBuilder readGroup = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName)
            .RequireAuthorization(IdentityPermissions.Users.Read);
        readGroup.MapCapabilitiesEndpoints();
        readGroup.MapReadEndpoints();
        readGroup.MapStatsEndpoints();

        RouteGroupBuilder syncGroup = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName)
            .RequireAuthorization(IdentityPermissions.Users.Sync);
        syncGroup.MapSyncEndpoints();

        RouteGroupBuilder gdprGroup = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName)
            .RequireAuthorization(IdentityPermissions.Users.Delete);
        gdprGroup.MapGdprEndpoints();

        // Webhook endpoint (outside the authorized group — uses signature validation)
        endpoints.MapWebhookEndpoint("", options.WebhookTagName);

        return readGroup;
    }
}
