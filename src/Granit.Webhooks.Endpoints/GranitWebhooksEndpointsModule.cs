using Granit.Authorization;
using Granit.Modularity;
using Granit.QueryEngine.AspNetCore;
using Granit.Webhooks;

namespace Granit.Webhooks.Endpoints;

/// <summary>
/// Granit module for webhook subscription administration HTTP endpoints.
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitQueryEngineAspNetCoreModule),
    typeof(GranitWebhooksModule))]
public sealed class GranitWebhooksEndpointsModule : GranitModule;
