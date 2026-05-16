using Granit.AI.Endpoints.Internal;
using Granit.AI.Endpoints.Workspaces;
using Granit.Authorization;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.QueryEngine.AspNetCore;
using Granit.Workspaces;
using Granit.Workspaces.Extensions;

namespace Granit.AI.Endpoints;

/// <summary>
/// Granit module for AI administration and inference HTTP endpoints.
/// </summary>
[DependsOn(
    typeof(GranitAIModule),
    typeof(GranitAuthorizationModule),
    typeof(GranitQueryEngineAspNetCoreModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitAIEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddLocalizationResource<AIEndpointsLocalizationResource>();
        context.Services.AddFeatureProvider<AIFeatureProvider>();
    }
}
