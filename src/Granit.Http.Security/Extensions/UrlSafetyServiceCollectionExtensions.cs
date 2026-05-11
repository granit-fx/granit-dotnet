using Granit.Http.Security.Internal;
using Granit.Http.Security.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Http.Security.Extensions;

/// <summary>
/// DI extensions for <see cref="IUrlSafetyValidator"/>.
/// </summary>
public static class UrlSafetyServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IUrlSafetyValidator"/> bound to <see cref="UrlSafetyOptions"/>
    /// (configuration section <c>Http:UrlSafety</c>).
    /// </summary>
    public static IServiceCollection AddGranitUrlSafety(
        this IServiceCollection services,
        Action<UrlSafetyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<UrlSafetyOptions>()
            .BindConfiguration(UrlSafetyOptions.SectionName)
            .ValidateOnStart();

        if (configure is not null)
        {
            services.PostConfigure(configure);
        }

        services.TryAddSingleton<IDnsResolver, SystemDnsResolver>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IUrlSafetyValidator, DefaultUrlSafetyValidator>();
        return services;
    }
}
