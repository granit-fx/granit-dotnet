using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;

namespace Granit.MultiTenancy.Endpoints;

/// <summary>
/// Granit module for multi-tenant management HTTP endpoints.
/// </summary>
/// <remarks>
/// Exposes tenant CRUD and activation/deactivation endpoints under
/// <c>/{prefix}/multi-tenancy/tenants</c> (<see cref="Extensions.MultiTenancyEndpointRouteBuilderExtensions.MapGranitMultiTenancy"/>).
/// Permission definition providers are auto-discovered by the authorization module.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitMultiTenancyModule),
    typeof(GranitValidationModule))]
public sealed class GranitMultiTenancyEndpointsModule : GranitModule;
