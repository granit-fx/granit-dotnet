using Granit.Http.Cookies;
using Granit.Privacy.Regulations.Cookies.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Privacy.Regulations.Cookies.Extensions;

/// <summary>
/// Extensions for registering the regulation-cookies bridge.
/// </summary>
public static class RegulationCookiesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the regulation-aware cookie consent model provider, replacing the default
    /// <c>NullCookieConsentModelProvider</c>. When loaded, the <see cref="Granit.Http.Cookies.Internal.GranitCookieManager"/>
    /// will consult the tenant's regulation profile to determine GPC enforcement and consent model.
    /// </summary>
    public static IServiceCollection AddGranitRegulationCookiesBridge(this IServiceCollection services)
    {
        // Replace the default NullCookieConsentModelProvider with regulation-aware provider
        services.AddScoped<ICookieConsentModelProvider, RegulationBasedCookieConsentModelProvider>();
        return services;
    }
}
