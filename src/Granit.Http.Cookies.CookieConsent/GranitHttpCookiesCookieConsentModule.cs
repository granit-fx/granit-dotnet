using Granit.Http.Cookies.CookieConsent.Extensions;
using Granit.Modularity;

namespace Granit.Http.Cookies.CookieConsent;

/// <summary>
/// Granit module for the @cookieconsent/core CMP integration.
/// Depends on <see cref="GranitHttpCookiesModule"/> for the cookie management infrastructure.
/// Registration is done via <see cref="GranitCookiesBuilderExtensions.UseCookieConsent"/>
/// or automatically through the module system.
/// </summary>
[DependsOn(typeof(GranitHttpCookiesModule))]
public sealed class GranitHttpCookiesCookieConsentModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitCookiesCookieConsent();
}
