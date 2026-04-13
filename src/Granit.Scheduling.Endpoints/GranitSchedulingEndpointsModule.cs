using Granit.Authorization;
using Granit.Modularity;
using Granit.QueryEngine.AspNetCore;
using Granit.QueryEngine.Extensions;
using Granit.Scheduling.Domain;
using Granit.Scheduling.Endpoints.Queries;
using Granit.Validation;

namespace Granit.Scheduling.Endpoints;

/// <summary>
/// Granit module for scheduled actions administration HTTP endpoints.
/// </summary>
/// <remarks>
/// Exposes management routes via
/// <see cref="Extensions.SchedulingEndpointRouteBuilderExtensions.MapGranitScheduling"/>.
/// Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.
/// The list/search endpoint uses <c>MapGranitQuery</c> for pagination, filtering, and sorting.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitQueryEngineAspNetCoreModule),
    typeof(GranitSchedulingModule),
    typeof(GranitValidationModule))]
public sealed class GranitSchedulingEndpointsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddQueryDefinition<ScheduledAction, ScheduledActionQueryDefinition>();
    }
}
