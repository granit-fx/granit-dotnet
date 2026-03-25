using Granit.Auditing.Diagnostics;
using Granit.Auditing.Options;
using Granit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Auditing.Extensions;

/// <summary>
/// Extensions for configuring Granit.Auditing services in the DI container.
/// </summary>
public static class AuditingServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Auditing module services: options binding and activity source registration.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="configure">Optional delegate to customize <see cref="AuditingOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitAuditing(
        this IServiceCollection services,
        Action<AuditingOptions>? configure = null)
    {
        services
            .AddOptions<AuditingOptions>()
            .BindConfiguration(AuditingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        GranitActivitySourceRegistry.Register(AuditingActivitySource.Name);

        return services;
    }
}
