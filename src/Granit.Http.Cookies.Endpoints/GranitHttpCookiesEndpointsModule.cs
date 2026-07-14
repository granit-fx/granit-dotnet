using Granit.Http.ApiDocumentation;
using Granit.Http.Cookies.Endpoints.Internal;
using Granit.Http.Cookies.Endpoints.Options;
using Granit.Http.RateLimiting;
using Granit.Http.UrlSafety;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Http.Cookies.Endpoints;

/// <summary>
/// Granit module for cookie consent configuration endpoints.
/// </summary>
/// <remarks>
/// Exposes <c>GET /cookies/config</c> and <c>POST /cookies/consent</c> via
/// <see cref="Extensions.CookieConsentEndpointRouteBuilderExtensions.MapGranitCookieConsent"/>.
/// </remarks>
[DependsOn(
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitHttpCookiesModule),
    typeof(GranitHttpRateLimitingModule),
    typeof(GranitHttpUrlSafetyModule))]
public sealed class GranitHttpCookiesEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services
            .AddOptions<CookieConsentEndpointsOptions>()
            .BindConfiguration(CookieConsentEndpointsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // DecidedAt capture in the consent endpoint (testable clock).
        context.Services.TryAddSingleton(TimeProvider.System);

        context.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ISchemaExampleProvider, CookiesSchemaExampleProvider>());
    }
}
