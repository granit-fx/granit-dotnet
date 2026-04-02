using System.Threading.Channels;
using Granit.Auditing.Diagnostics;
using Granit.Auditing.Internal.Services;
using Granit.Auditing.Messages;
using Granit.Auditing.Options;
using Granit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.Auditing.Extensions;

/// <summary>
/// Extensions for configuring Granit.Auditing services in the DI container.
/// </summary>
public static class AuditingServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Auditing module services: options binding, activity source registration,
    /// metrics, async channel, publisher, and background workers.
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

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<AuditingOptions>, AuditingOptionsValidator>());

        if (configure is not null)
        {
            services.Configure(configure);
        }

        GranitActivitySourceRegistry.Register(AuditingActivitySource.Name);

        // Metrics.
        services.TryAddSingleton<AuditingMetrics>();

        // Async persistence channel with backpressure.
        services.AddSingleton(sp =>
        {
            AuditingOptions opts = sp.GetRequiredService<IOptions<AuditingOptions>>().Value;
            return Channel.CreateBounded<AuditingBatch>(new BoundedChannelOptions(opts.ChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
            });
        });

        // Channel publisher (concrete type — IAuditEntryPublisher is wired by the persistence layer).
        services.AddScoped<ChannelAuditingPublisher>();

        // Background workers.
        services.AddHostedService<AuditingPersistenceWorker>();
        services.AddHostedService<AuditingCleanupWorker>();

        return services;
    }
}
