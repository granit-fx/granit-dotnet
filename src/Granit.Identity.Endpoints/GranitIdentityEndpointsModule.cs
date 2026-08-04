using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Identity.Endpoints.Internal;
using Granit.Identity.Endpoints.Workspaces;
using Granit.IpGeolocation;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.Validation;
using Granit.Workspaces;
using Granit.Workspaces.Extensions;

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
/// <see cref="Extensions.IdentityEndpointRouteBuilderExtensions.MapGranitIdentityUserCache"/>.
/// Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitIdentityAbstractionsModule),
    typeof(GranitIdentityModule),
    typeof(GranitIpGeolocationModule),
    typeof(GranitValidationModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitIdentityEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddLocalizationResource<IdentityEndpointsLocalizationResource>();
        context.Services.AddFeatureProvider<IdentityFeatureProvider>();
    }
}
