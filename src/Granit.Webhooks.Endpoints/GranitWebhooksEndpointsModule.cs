using Granit.Authorization;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.QueryEngine.AspNetCore;
using Granit.Webhooks.Endpoints.Internal;
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
        context.Services.AddLocalizationResource<WebhooksEndpointsLocalizationResource>();
        context.Services.AddFeatureProvider<WebhooksFeatureProvider>();
    }
}
