using Granit.Auditing;
using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.OpenIddict.Endpoints.Internal;
using Granit.OpenIddict.Endpoints.Options;
using Granit.OpenIddict.Endpoints.Workspaces;
using Granit.OpenIddict.Server;
using Granit.QueryEngine;
using Granit.Validation;
using Granit.Workspaces;
using Granit.Workspaces.Extensions;
using Microsoft.Extensions.DependencyInjection;

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
    typeof(GranitAuditingModule),
    typeof(GranitAuthorizationModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitOpenIddictModule),
    typeof(GranitOpenIddictServerModule),
    typeof(GranitQueryEngineAbstractionsModule),
    typeof(GranitValidationModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitOpenIddictEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddLocalizationResource<OpenIddictEndpointsLocalizationResource>();
        context.Services.AddFeatureProvider<OpenIddictFeatureProvider>();

        // Bind the endpoint options from configuration so appsettings keys are honoured at request
        // time. The MapGranitOpenIddict* delegates seed from these before applying any imperative
        // overrides; ValidateOnStart surfaces a bad section at boot.
        context.Services.AddOptions<OpenIddictEndpointsOptions>()
            .BindConfiguration(OpenIddictEndpointsOptions.SectionName)
            .ValidateOnStart();

        context.Services.AddOptions<OpenIddictServerEndpointsOptions>()
            .BindConfiguration(OpenIddictServerEndpointsOptions.SectionName)
            .ValidateOnStart();
    }
}
