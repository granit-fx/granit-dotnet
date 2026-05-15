using Granit.Authorization;
using Granit.Modularity;
using Granit.QueryEngine.AspNetCore;
using Granit.Webhooks;
using Granit.Webhooks.Endpoints.Workspaces;
using Granit.Workspaces;
using Granit.Workspaces.Extensions;

namespace Granit.Webhooks.Endpoints;

/// <summary>
/// Granit module for webhook subscription administration HTTP endpoints.
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitQueryEngineAspNetCoreModule),
    typeof(GranitWebhooksModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitWebhooksEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddFeatureProvider<WebhooksFeatureProvider>();
    }
}
