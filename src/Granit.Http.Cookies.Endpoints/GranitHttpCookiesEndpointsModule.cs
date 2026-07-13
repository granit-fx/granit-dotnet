using Granit.Http.ApiDocumentation;
using Granit.Http.Cookies.Endpoints.Internal;
using Granit.Http.Cookies.Endpoints.Options;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        context.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ISchemaExampleProvider, CookiesSchemaExampleProvider>());
    }
}
