using Granit.Authorization;
using Granit.Guids;
using Granit.Http.ApiDocumentation;
using Granit.Http.Cookies;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.Privacy.Endpoints.Discovery;
using Granit.Privacy.Endpoints.Internal;
using Granit.Privacy.Endpoints.Workspaces;
using Granit.Privacy.Regulations;
using Granit.Validation;
using Granit.Workspaces;
using Granit.Workspaces.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Privacy.Endpoints;

/// <summary>
/// Granit module for privacy endpoints: personal data export, data deletion,
/// legal agreement consent management, regulation profile resolution, and the
/// optional Global Privacy Control (GPC) public discovery resource.
/// </summary>
/// <remarks>
/// <para>Exposes the following endpoint groups:</para>
/// <list type="bullet">
/// <item><see cref="Extensions.PrivacyEndpointRouteBuilderExtensions.MapGranitPrivacy"/> — regulation, export, deletion, agreements (authenticated, under the privacy prefix).</item>
/// <item><see cref="GpcDiscoveryEndpointRouteBuilderExtensions.MapGranitPrivacyGpcDiscovery"/> — opt-in <c>/.well-known/gpc.json</c> at the host root (anonymous, excluded from OpenAPI).</item>
/// </list>
/// <para>
/// <b>Reverse proxy note:</b> The consent acceptance endpoint reads the client IP address
/// from <c>HttpContext.Connection.RemoteIpAddress</c> for the GDPR Art. 7 audit trail.
/// Ensure <c>ForwardedHeadersMiddleware</c> is enabled when running behind a reverse proxy.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitGuidsModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitHttpCookiesModule),
    typeof(GranitPrivacyModule),
    typeof(GranitPrivacyRegulationsModule),
    typeof(GranitValidationModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitPrivacyEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddLocalizationResource<PrivacyEndpointsLocalizationResource>();
        context.Services.AddFeatureProvider<PrivacyFeatureProvider>();

        context.Services
            .AddOptions<GpcDiscoveryOptions>()
            .BindConfiguration(GpcDiscoveryOptions.SectionName)
            .ValidateOnStart();
        context.Services.AddSingleton<IValidateOptions<GpcDiscoveryOptions>, GpcDiscoveryOptionsValidator>();
    }
}
