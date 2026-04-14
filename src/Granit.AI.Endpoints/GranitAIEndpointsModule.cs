using Granit.AI.Endpoints.Queries;
using Granit.Authorization;
using Granit.Modularity;
using Granit.QueryEngine.AspNetCore;
using Granit.QueryEngine.Extensions;

namespace Granit.AI.Endpoints;

/// <summary>
/// Granit module for AI administration and inference HTTP endpoints.
/// </summary>
[DependsOn(
    typeof(GranitAIModule),
    typeof(GranitAuthorizationModule),
    typeof(GranitQueryEngineAspNetCoreModule))]
public sealed class GranitAIEndpointsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddQueryDefinition<AIUsageRecord, AIUsageRecordQueryDefinition>();
}
