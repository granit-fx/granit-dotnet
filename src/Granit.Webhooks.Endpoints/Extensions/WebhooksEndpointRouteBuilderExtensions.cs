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
        // TODO(VULN-001): cross-tenant reads are now fail-closed by default. A host admin with no
        // resolved tenant sees only the host partition — add .AllowHostAccess() + a host-scoped
        // permission on these groups if cross-tenant webhook visibility is intended.
        group.MapGranitGroup("subscriptions").MapGranitQuery<WebhookSubscription>();
        group.MapGranitGroup("deliveries").MapGranitQuery<WebhookDeliveryAttempt>();

        return group;
    }

}
