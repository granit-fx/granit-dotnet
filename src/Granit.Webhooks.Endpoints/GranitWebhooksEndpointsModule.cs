using Granit.Authorization;
using Granit.Modularity;
using Granit.QueryEngine.AspNetCore;
using Granit.QueryEngine.Extensions;
using Granit.Webhooks;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Endpoints.Queries;

namespace Granit.Webhooks.Endpoints;

/// <summary>
/// Granit module for webhook subscription administration HTTP endpoints.
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitQueryEngineAspNetCoreModule),
    typeof(GranitWebhooksModule))]
public sealed class GranitWebhooksEndpointsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddQueryDefinition<WebhookSubscription, WebhookSubscriptionQueryDefinition>();
        context.Services.AddQueryDefinition<WebhookDeliveryAttempt, WebhookDeliveryAttemptQueryDefinition>();
    }
}
