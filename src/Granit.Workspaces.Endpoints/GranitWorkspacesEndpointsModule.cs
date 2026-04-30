using Granit.Authorization;
using Granit.Caching;
using Granit.Modularity;
using Granit.Workspaces.Endpoints.Extensions;

namespace Granit.Workspaces.Endpoints;

/// <summary>
/// Granit module for the workspace HTTP surface (per ADR-040, Phase 1.D).
/// Mounts <c>GET /api/workspaces</c> via <c>MapGranitWorkspacesEndpoints</c>.
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitCachingModule),
    typeof(GranitWorkspacesModule))]
public sealed class GranitWorkspacesEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitWorkspacesEndpoints();
}
