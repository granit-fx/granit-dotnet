using Granit.QueryEngine.AspNetCore.Extensions;
using Granit.Validation.AspNetCore;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Endpoints.Endpoints;
using Granit.Webhooks.Endpoints.Options;
using Granit.Webhooks.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Webhooks.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering webhook administration endpoints.
/// </summary>
public static class WebhooksEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the webhook administration endpoints onto the given route builder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Requires the <c>Webhooks.Subscriptions.Read</c> permission on the route group.
    /// Individual endpoints may require additional permissions (e.g. <c>Webhooks.Subscriptions.Manage</c>).
    /// </para>
    /// <para>Call this from your application route registration:</para>
    /// <code>
    /// app.MapGranitWebhooks();
    ///
    /// // With custom options:
    /// app.MapGranitWebhooks(opts =>
    /// {
    ///     opts.RoutePrefix = "admin/webhooks";
    /// });
    /// </code>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="WebhooksEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitWebhooks(
        this IEndpointRouteBuilder endpoints,
        Action<WebhooksEndpointsOptions>? configure = null)
    {
        WebhooksEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName)
            .RequireAuthorization(WebhooksPermissions.Subscriptions.Read);

        group.MapEventTypeEndpoints();
        group.MapReadEndpoints();

        // Signing key list — Read permission inherited from the parent group.
        group.MapSigningKeyReadEndpoints();

        // Mutating operations require Manage permission (ISO 27001 A.5.15 — least privilege).
        group.MapWriteEndpoints()
            .RequireAuthorization(WebhooksPermissions.Subscriptions.Manage);
        group.MapLifecycleEndpoints()
            .RequireAuthorization(WebhooksPermissions.Subscriptions.Manage);
        group.MapOperationEndpoints()
            .RequireAuthorization(WebhooksPermissions.Subscriptions.Manage);
        group.MapSigningKeyWriteEndpoints()
            .RequireAuthorization(WebhooksPermissions.Subscriptions.Manage);

        // Query endpoints for subscription list and delivery attempts.
        // Cross-tenant reads are fail-closed by default: a host operator with no resolved tenant
        // sees only the host partition. To expose cross-tenant webhook visibility, mark these
        // groups .AllowHostAccess(); a platform admin holding Subscriptions.Read at global scope
        // then reads across tenants, while the multi-tenant filter stays enforced for tenant callers.
        group.MapGranitGroup("subscriptions").MapGranitQuery<WebhookSubscription>();
        group.MapGranitGroup("deliveries").MapGranitQuery<WebhookDeliveryAttempt>();

        return group;
    }

}
