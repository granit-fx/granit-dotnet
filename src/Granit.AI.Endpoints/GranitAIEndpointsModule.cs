using Granit.Authorization;
using Granit.Modularity;
using Granit.QueryEngine.Endpoints;

namespace Granit.AI.Endpoints;

/// <summary>
/// Granit module for AI administration and inference HTTP endpoints.
/// </summary>
[DependsOn(
    typeof(GranitAIModule),
    typeof(GranitAuthorizationModule),
    typeof(GranitQueryEngineEndpointsModule))]
public sealed class GranitAIEndpointsModule : GranitModule;
