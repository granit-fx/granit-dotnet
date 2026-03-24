using Granit.AuditLog.Diagnostics;
using Granit.AuditLog.Options;
using Granit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.AuditLog.Extensions;

/// <summary>
/// Extensions for configuring Granit.AuditLog services in the DI container.
/// </summary>
public static class AuditLogServiceCollectionExtensions
{
    /// <summary>
    /// Adds the AuditLog module services: options binding and activity source registration.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="configure">Optional delegate to customize <see cref="AuditLogOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitAuditLog(
        this IServiceCollection services,
        Action<AuditLogOptions>? configure = null)
    {
        services
            .AddOptions<AuditLogOptions>()
            .BindConfiguration(AuditLogOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        GranitActivitySourceRegistry.Register(AuditLogActivitySource.Name);

        return services;
    }
}
