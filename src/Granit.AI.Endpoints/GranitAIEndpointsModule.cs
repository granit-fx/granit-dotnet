using Granit.Authorization;
using Granit.Core.Modularity;
using Granit.Querying.Endpoints;

namespace Granit.AI.Endpoints;

/// <summary>
/// Granit module for AI administration and inference HTTP endpoints.
/// </summary>
[DependsOn(
    typeof(GranitAIModule),
    typeof(GranitAuthorizationModule),
    typeof(GranitQueryingEndpointsModule))]
public sealed class GranitAIEndpointsModule : GranitModule;
