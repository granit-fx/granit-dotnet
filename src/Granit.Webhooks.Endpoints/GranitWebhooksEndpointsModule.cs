using Granit.Authorization;
using Granit.Core.Modularity;
using Granit.Querying.Endpoints;
using Granit.Webhooks;

namespace Granit.Webhooks.Endpoints;

/// <summary>
/// Granit module for webhook subscription administration HTTP endpoints.
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitQueryingEndpointsModule),
    typeof(GranitWebhooksModule))]
public sealed class GranitWebhooksEndpointsModule : GranitModule;
