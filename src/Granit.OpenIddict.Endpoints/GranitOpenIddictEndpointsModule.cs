using Granit.Authorization;
using Granit.Caching;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.OpenIddict.Endpoints.Internal;
using Granit.QueryEngine;
using Granit.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.OpenIddict.Endpoints;

/// <summary>
/// Module for OpenIddict REST API endpoints: account self-service (<c>/api/account</c>),
/// admin management (<c>/api/admin/oidc</c>), and OIDC server protocol endpoints
/// (<c>/connect/*</c>).
/// </summary>
/// <remarks>
/// <para>Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.</para>
/// <para>
/// <b>Middleware order (mandatory):</b>
/// <c>UseAuthentication()</c> → <c>UseOpenIddict()</c> → <c>UseAuthorization()</c>
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitCachingModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitOpenIddictModule),
    typeof(GranitQueryEngineAbstractionsModule),
    typeof(GranitValidationModule))]
public sealed class GranitOpenIddictEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddScoped<OidcPrincipalFactory>();
    }
}
