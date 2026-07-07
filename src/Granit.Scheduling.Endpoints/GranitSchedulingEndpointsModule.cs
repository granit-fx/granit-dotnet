using Granit.Authorization;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.QueryEngine.Endpoints;
using Granit.Scheduling.Endpoints.Internal;
using Granit.Scheduling.Endpoints.Workspaces;
using Granit.Validation;
using Granit.Workspaces;
using Granit.Workspaces.Extensions;

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
    typeof(GranitQueryEngineEndpointsModule),
    typeof(GranitSchedulingModule),
    typeof(GranitValidationModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitSchedulingEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddLocalizationResource<SchedulingEndpointsLocalizationResource>();
        context.Services.AddFeatureProvider<SchedulingFeatureProvider>();
    }
}
