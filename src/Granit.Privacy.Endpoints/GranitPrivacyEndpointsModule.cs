using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;

namespace Granit.Privacy.Endpoints;

/// <summary>
/// Granit module for GDPR privacy endpoints: personal data export (Art. 15/20),
/// data deletion (Art. 17), and legal agreement consent management (Art. 7).
/// </summary>
/// <remarks>
/// <para>Exposes three endpoint groups via <see cref="Extensions.PrivacyEndpointRouteBuilderExtensions.MapGranitPrivacy"/>:</para>
/// <list type="bullet">
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
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitPrivacyModule),
    typeof(GranitValidationModule))]
public sealed class GranitPrivacyEndpointsModule : GranitModule;
