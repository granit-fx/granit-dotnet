using Granit.Http.Cookies.CookieConsent.Internal;
using Granit.Http.Cookies.CookieConsent.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        // Deterministic single-resolver rule: replacing the base module's
        // NullConsentResolver is expected, but two CMP packages registering
        // competing resolvers is a wiring error — fail loud instead of
        // resolving whichever happened to be registered last.
        ServiceDescriptor? existing = services.LastOrDefault(d => d.ServiceType == typeof(IConsentResolver));
        if (existing?.ImplementationType is { } impl
            && impl != typeof(CookieConsentConsentResolver)
            && impl.Assembly != typeof(IConsentResolver).Assembly)
        {
            throw new InvalidOperationException(
                $"An IConsentResolver from another CMP package is already registered ({impl.FullName}). " +
                "Reference a single CMP integration package.");
        }

        services.Replace(ServiceDescriptor.Scoped<IConsentResolver, CookieConsentConsentResolver>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ICookieDefinitionContributor, CookieConsentCookieDefinitionContributor>());

        return services;
    }
}
