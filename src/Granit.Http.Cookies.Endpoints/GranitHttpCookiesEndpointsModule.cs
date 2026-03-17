using Granit.Core.Modularity;
using Granit.Http.ApiDocumentation;

namespace Granit.Http.Cookies.Endpoints;

/// <summary>
/// Granit module for cookie consent configuration endpoints.
/// </summary>
/// <remarks>
/// Exposes <c>GET /cookies/config</c> via
/// <see cref="Extensions.CookieConsentEndpointRouteBuilderExtensions.MapGranitCookieConsent"/>.
/// </remarks>
[DependsOn(
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitHttpCookiesModule))]
public sealed class GranitHttpCookiesEndpointsModule : GranitModule;
