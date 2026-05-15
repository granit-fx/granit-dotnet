using Granit.Authorization;
using Granit.BackgroundJobs.Endpoints.Workspaces;
using Granit.Modularity;
using Granit.QueryEngine;
using Granit.Validation;
using Granit.Workspaces;
using Granit.Workspaces.Extensions;

namespace Granit.BackgroundJobs.Endpoints;

/// <summary>
/// Granit module for background jobs administration HTTP endpoints.
/// </summary>
/// <remarks>
/// Exposes job management routes via
/// <see cref="Extensions.BackgroundJobsEndpointRouteBuilderExtensions.MapGranitBackgroundJobs"/>.
/// Requires both <see cref="GranitBackgroundJobsModule"/> (job store and manager)
/// and <see cref="GranitAuthorizationModule"/> (permission policy enforcement).
/// Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitBackgroundJobsModule),
    typeof(GranitQueryEngineAbstractionsModule),
    typeof(GranitValidationModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitBackgroundJobsEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddFeatureProvider<BackgroundJobsFeatureProvider>();
    }
}
