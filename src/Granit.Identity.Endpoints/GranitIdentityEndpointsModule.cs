using Granit.Authorization;
using Granit.Core.Modularity;
using Granit.Http.ApiDocumentation;
using Granit.Validation;

namespace Granit.Identity.Endpoints;

/// <summary>
/// Granit module for identity user cache Minimal API endpoints.
/// </summary>
/// <remarks>
/// <para>
/// Depends only on <see cref="GranitIdentityModule"/> (abstractions) and
/// <see cref="GranitAuthorizationModule"/> (permission policy enforcement).
/// Does <b>not</b> depend on <c>Granit.Identity.Federated.EntityFrameworkCore</c> —
/// the host application is responsible for registering the EF Core implementation.
/// </para>
/// <para>
/// Exposes user cache management routes via
/// <see cref="Extensions.IdentityEndpointRouteBuilderExtensions.MapIdentityUserCacheEndpoints"/>.
/// Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitIdentityModule),
    typeof(GranitValidationModule))]
public sealed class GranitIdentityEndpointsModule : GranitModule;
