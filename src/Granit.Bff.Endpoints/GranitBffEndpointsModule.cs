using Granit.Http.ApiDocumentation;
using Granit.Http.Cookies;
using Granit.Http.Cookies.Extensions;
using Granit.Modularity;
using Granit.Validation;

namespace Granit.Bff.Endpoints;

/// <summary>
/// Granit module for BFF authentication HTTP endpoints (login, callback, logout, user, CSRF).
/// </summary>
[DependsOn(
    typeof(GranitBffModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitHttpCookiesModule),
    typeof(GranitValidationModule))]
public sealed class GranitBffEndpointsModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Ensure IGranitCookieManager + ICookieRegistry are available for BFF session cookies.
        // No-op if AddGranitCookies() was already called by the hosting application.
        context.Services.AddGranitCookies(_ => { });
    }
}
