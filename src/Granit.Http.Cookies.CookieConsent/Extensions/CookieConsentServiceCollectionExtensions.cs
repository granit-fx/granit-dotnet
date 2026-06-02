using Granit.Http.Cookies.CookieConsent.Internal;
using Granit.Http.Cookies.CookieConsent.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Http.Cookies.CookieConsent.Extensions;

/// <summary>
/// Extension methods for registering the @cookieconsent/core CMP integration.
/// </summary>
public static class CookieConsentServiceCollectionExtensions
{
    /// <summary>
    /// Registers the CookieConsent consent resolver and binds <see cref="CookieConsentOptions"/>
    /// from the <c>Http:Cookies:CookieConsent</c> configuration section.
    /// </summary>
    public static IServiceCollection AddGranitCookiesCookieConsent(this IServiceCollection services)
    {
        services.AddOptions<CookieConsentOptions>()
            .BindConfiguration(CookieConsentOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IConsentResolver, CookieConsentConsentResolver>();
        services.AddSingleton<ICookieDefinitionContributor, CookieConsentCookieDefinitionContributor>();

        return services;
    }
}
