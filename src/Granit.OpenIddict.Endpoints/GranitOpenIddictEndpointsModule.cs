using Granit.Authorization;
using Granit.Caching;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.QueryEngine;
using Granit.Validation;

namespace Granit.OpenIddict.Endpoints;

/// <summary>
/// Module for OpenIddict REST API endpoints: account self-service (<c>/api/account</c>)
/// and admin management (<c>/api/admin/users</c>, <c>/api/admin/roles</c>,
/// <c>/api/admin/groups</c>, <c>/api/admin/oidc</c>).
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
    typeof(GranitQueryEngineModule),
    typeof(GranitValidationModule))]
public sealed class GranitOpenIddictEndpointsModule : GranitModule;
