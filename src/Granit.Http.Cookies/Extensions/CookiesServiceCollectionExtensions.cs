using Granit.Http.Cookies.Internal;
using Granit.Http.Cookies.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Http.Cookies.Extensions;

/// <summary>
/// Extensions for registering Granit.Http.Cookies module services.
/// </summary>
public static class CookiesServiceCollectionExtensions
{
    /// <summary>
    /// Adds Granit.Http.Cookies services (ICookieRegistry, IGranitCookieManager, IThirdPartyServiceRegistry)
    /// and registers cookie definitions declared in the builder.
    /// </summary>
    public static IServiceCollection AddGranitCookies(
        this IServiceCollection services,
        Action<GranitCookiesBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<GranitCookiesOptions>()
            .BindConfiguration(GranitCookiesOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        CookieRegistry registry = new();
        GranitCookiesBuilder builder = new(services);
        configure(builder);

        foreach (CookieDefinition definition in builder.CookieDefinitions)
        {
            registry.Register(definition);
        }

        services.TryAddSingleton<ICookieRegistry>(registry);
        services.TryAddScoped<IConsentResolver, NullConsentResolver>();
        services.TryAddSingleton<IGlobalPrivacyControlSignal, GlobalPrivacyControlHeaderSignal>();
        services.TryAddScoped<ICookieConsentModelProvider, NullCookieConsentModelProvider>();
        services.TryAddScoped<IGranitCookieManager, GranitCookieManager>();

        // Third-party service registry — populated from configuration
        services.TryAddSingleton<IThirdPartyServiceRegistry>(sp =>
        {
            IConfiguration configuration = sp.GetRequiredService<IConfiguration>();
            GranitCookiesOptions options = new();
            configuration.GetSection(GranitCookiesOptions.SectionName).Bind(options);

            var definitions = options.ThirdPartyServices
                .Select(s => new ThirdPartyServiceDefinition(s.Name, s.Category, s.CookiePatterns))
                .ToList();

            return new ThirdPartyServiceRegistry(definitions);
        });

        return services;
    }
}
