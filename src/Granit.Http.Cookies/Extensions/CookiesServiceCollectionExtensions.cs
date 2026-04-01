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
    /// and registers cookie definitions declared in the builder and contributed by modules.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method can be called multiple times safely. Cookie definitions from each call
    /// accumulate additively. The <see cref="ICookieRegistry"/> is built lazily at first
    /// resolution, collecting definitions from:
    /// </para>
    /// <list type="number">
    /// <item>Builder callback definitions (from every <c>AddGranitCookies()</c> call)</item>
    /// <item><see cref="ICookieDefinitionContributor"/> implementations registered by modules</item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddGranitCookies(
        this IServiceCollection services,
        Action<GranitCookiesBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<GranitCookiesOptions>()
            .BindConfiguration(GranitCookiesOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        GranitCookiesBuilder builder = new(services);
        configure(builder);

        // Additive registration — each call accumulates its definitions.
        // GetServices<CookieDefinition>() collects them all at resolution time.
        foreach (CookieDefinition definition in builder.CookieDefinitions)
        {
            services.AddSingleton(definition);
        }

        // Lazy factory — collects ALL definitions + contributors after DI build.
        // TryAddSingleton ensures only the first factory wins, but it sees everything
        // because GetServices<T>() returns all registrations regardless of call order.
        services.TryAddSingleton<ICookieRegistry>(sp =>
        {
            CookieRegistry registry = new();

            foreach (CookieDefinition definition in sp.GetServices<CookieDefinition>())
            {
                registry.Register(definition);
            }

            foreach (ICookieDefinitionContributor contributor in
                sp.GetServices<ICookieDefinitionContributor>())
            {
                foreach (CookieDefinition definition in contributor.GetCookieDefinitions())
                {
                    registry.Register(definition);
                }
            }

            return registry;
        });

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
