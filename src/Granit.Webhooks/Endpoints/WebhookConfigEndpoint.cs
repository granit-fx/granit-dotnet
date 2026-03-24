using Granit.Endpoints;
using Granit.Webhooks.Dtos;
using Microsoft.AspNetCore.Routing;

namespace Granit.Webhooks.Endpoints;

/// <summary>
/// Convenience extension that maps the webhook module config endpoint
/// using the shared <see cref="ModuleConfigEndpointExtensions"/> pattern.
/// </summary>
public static class WebhookConfigEndpoint
{
    /// <summary>
    /// Maps <c>GET /webhooks/config</c> (or custom prefix).
    /// </summary>
    public static IEndpointRouteBuilder MapGranitWebhooksConfig(
        this IEndpointRouteBuilder endpoints,
        string routePrefix = "webhooks") =>
        endpoints.MapGranitModuleConfig<WebhookModuleConfigProvider, WebhookModuleConfigResponse>(
            routePrefix, "GetWebhooksConfig", "Webhooks");
}
