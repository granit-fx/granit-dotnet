using Granit.Http.Cookies.Klaro.Internal;
using Granit.Http.Cookies.Klaro.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Http.Cookies.Klaro.Extensions;

/// <summary>
/// Extension methods for registering the Klaro CMP integration.
/// </summary>
public static class KlaroServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Klaro consent resolver and binds <see cref="KlaroOptions"/>
    /// from the <c>Klaro</c> configuration section.
    /// </summary>
    public static IServiceCollection AddGranitCookiesKlaro(this IServiceCollection services)
    {
        services.AddOptions<KlaroOptions>()
            .BindConfiguration(KlaroOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Deterministic single-resolver rule — see CookieConsentServiceCollectionExtensions.
        ServiceDescriptor? existing = services.LastOrDefault(d => d.ServiceType == typeof(IConsentResolver));
        if (existing?.ImplementationType is { } impl
            && impl != typeof(KlaroConsentResolver)
            && impl.Assembly != typeof(IConsentResolver).Assembly)
        {
            throw new InvalidOperationException(
                $"An IConsentResolver from another CMP package is already registered ({impl.FullName}). " +
                "Reference a single CMP integration package.");
        }

        services.Replace(ServiceDescriptor.Scoped<IConsentResolver, KlaroConsentResolver>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ICookieDefinitionContributor, KlaroCookieDefinitionContributor>());

        return services;
    }
}
