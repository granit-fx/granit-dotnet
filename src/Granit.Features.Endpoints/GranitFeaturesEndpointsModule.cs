using Granit.Authorization;
using Granit.Core.Modularity;
using Granit.Http.ApiDocumentation;
using Granit.Validation;

namespace Granit.Features.Endpoints;

/// <summary>
/// Granit module for feature management HTTP endpoints.
/// </summary>
/// <remarks>
/// Exposes three sets of endpoints:
/// <list type="bullet">
/// <item>Feature definitions — read-only listing of all declared feature groups and their features
///   (<see cref="Extensions.FeaturesEndpointRouteBuilderExtensions.MapGranitFeatures"/>).</item>
/// <item>Feature values — resolved values for the current tenant/plan context.</item>
/// <item>Feature overrides — tenant-level override CRUD (requires <c>Features.Manage</c>).</item>
/// </list>
/// Permission definition providers are auto-discovered by the authorization module.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitFeaturesModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitValidationModule))]
public sealed class GranitFeaturesEndpointsModule : GranitModule;
