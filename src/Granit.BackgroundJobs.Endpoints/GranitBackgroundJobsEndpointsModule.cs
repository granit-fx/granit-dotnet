using Granit.Authorization;
using Granit.Core.Modularity;
using Granit.Querying;
using Granit.Validation;

namespace Granit.BackgroundJobs.Endpoints;

/// <summary>
/// Granit module for background jobs administration HTTP endpoints.
/// </summary>
/// <remarks>
/// Exposes job management routes via
/// <see cref="Extensions.BackgroundJobsEndpointRouteBuilderExtensions.MapBackgroundJobsEndpoints"/>.
/// Requires both <see cref="GranitBackgroundJobsModule"/> (job store and manager)
/// and <see cref="GranitAuthorizationModule"/> (permission policy enforcement).
/// Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitBackgroundJobsModule),
    typeof(GranitQueryingModule),
    typeof(GranitValidationModule))]
public sealed class GranitBackgroundJobsEndpointsModule : GranitModule;
