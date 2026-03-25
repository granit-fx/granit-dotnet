using Granit.Authorization;
using Granit.Modularity;
using Granit.QueryEngine.Endpoints;
using Granit.Webhooks;

namespace Granit.Webhooks.Endpoints;

/// <summary>
/// Granit module for webhook subscription administration HTTP endpoints.
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitQueryEngineEndpointsModule),
    typeof(GranitWebhooksModule))]
public sealed class GranitWebhooksEndpointsModule : GranitModule;
