using Granit.Authorization;
using Granit.Caching;
using Granit.Entities.Endpoints.Extensions;
using Granit.Modularity;

namespace Granit.Entities.Endpoints;

/// <summary>
/// Granit module for the entity-manifest HTTP surface (ADR-040, Phase 1.C).
/// Mounts <c>GET /api/entities</c> (discovery tree) and
/// <c>GET /api/entities/{name}</c> (per-entity manifest) via
/// <c>MapGranitEntitiesEndpoints</c>.
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitCachingModule),
    typeof(GranitEntitiesModule))]
public sealed class GranitEntitiesEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitEntitiesEndpoints();
}
