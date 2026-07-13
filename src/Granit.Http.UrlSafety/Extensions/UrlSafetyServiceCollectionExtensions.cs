using Granit.Diagnostics;
using Granit.Http.UrlSafety.Diagnostics;
using Granit.Http.UrlSafety.Internal;
using Granit.Http.UrlSafety.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Http.UrlSafety.Extensions;

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
            .ValidateDataAnnotations()
            .Validate(
                o => o.DnsResolveTimeout > TimeSpan.Zero && o.DnsResolveTimeout <= TimeSpan.FromSeconds(30),
                $"{nameof(UrlSafetyOptions.DnsResolveTimeout)} must be in (0, 30s].")
            .Validate(
                o => o.AllowedSchemes.Count > 0,
                $"{nameof(UrlSafetyOptions.AllowedSchemes)} must contain at least one entry.")
            .ValidateOnStart();

        if (configure is not null)
        {
            services.PostConfigure(configure);
        }

        services.TryAddSingleton<IDnsResolver, SystemDnsResolver>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<UrlSafetyMetrics>();
        services.TryAddSingleton<IUrlSafetyValidator, DefaultUrlSafetyValidator>();

        GranitActivitySourceRegistry.Register(UrlSafetyActivitySource.Name);
        return services;
    }
}
