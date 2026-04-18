using Granit.Authorization;
using Granit.Modularity;
using Granit.QueryEngine.AspNetCore;

namespace Granit.AI.Endpoints;

/// <summary>
/// Granit module for AI administration and inference HTTP endpoints.
/// </summary>
[DependsOn(
    typeof(GranitAIModule),
    typeof(GranitAuthorizationModule),
    typeof(GranitQueryEngineAspNetCoreModule))]
public sealed class GranitAIEndpointsModule : GranitModule;
