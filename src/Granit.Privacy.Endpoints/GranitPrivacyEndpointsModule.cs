using Granit.Authorization;
using Granit.Guids;
using Granit.Http.ApiDocumentation;
using Granit.Http.Cookies;
using Granit.Modularity;
using Granit.Privacy.Regulations;
using Granit.Validation;

namespace Granit.Privacy.Endpoints;

/// <summary>
/// Granit module for privacy endpoints: personal data export, data deletion,
/// legal agreement consent management, and regulation profile resolution.
/// </summary>
/// <remarks>
/// <para>Exposes four endpoint groups via <see cref="Extensions.PrivacyEndpointRouteBuilderExtensions.MapGranitPrivacy"/>:</para>
/// <list type="bullet">
/// <item>Regulation — returns the applicable regulation profile for the current tenant.</item>
/// <item>Export — triggers the scatter-gather saga and reports export status.</item>
/// <item>Deletion — publishes the distributed deletion event.</item>
/// <item>Agreements — lists legal documents, records consent, checks user status.</item>
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
    typeof(GranitValidationModule))]
public sealed class GranitPrivacyEndpointsModule : GranitModule;
