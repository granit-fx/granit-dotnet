using Granit.Bff.Options;
using Granit.Caching;
using Granit.Http.ApiDocumentation;
using Granit.Http.Cookies;
using Granit.Modularity;
using Granit.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Granit.Bff.Endpoints;

/// <summary>
/// Granit module for BFF authentication HTTP endpoints (login, callback, logout, user, CSRF).
/// </summary>
/// <remarks>
/// Cookie infrastructure is ensured by <see cref="GranitHttpCookiesModule"/> (dependency).
/// BFF session cookies are registered at route-mapping time in <c>MapGranitBff()</c>
/// because their names are configuration-driven (one per frontend).
/// </remarks>
[DependsOn(
    typeof(GranitBffModule),
    typeof(GranitCachingModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitHttpCookiesModule),
    typeof(GranitValidationModule))]
public sealed class GranitBffEndpointsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        bool isDevelopment = context.Builder!.Environment.IsDevelopment();

        // In development (HTTP), use "." prefix instead of "__Host-" on BFF session cookies.
        // __Host- requires HTTPS — browsers silently ignore the cookie on HTTP.
        context.Services.PostConfigureAll<BffFrontendOptions>(options =>
        {
            options.CookiePrefix = isDevelopment ? "." : "__Host-";
        });
    }
}
