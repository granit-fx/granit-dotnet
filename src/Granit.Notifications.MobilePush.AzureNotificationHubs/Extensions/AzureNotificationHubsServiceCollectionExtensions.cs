using Granit.Core.Diagnostics;
using Granit.Notifications.MobilePush.AzureNotificationHubs.Diagnostics;
using Granit.Notifications.MobilePush.AzureNotificationHubs.HealthChecks;
using Granit.Notifications.MobilePush.AzureNotificationHubs.Internal;
using Granit.Notifications.MobilePush.AzureNotificationHubs.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.MobilePush.AzureNotificationHubs.Extensions;

/// <summary>Extension methods for the Azure Notification Hubs mobile push provider.</summary>
public static class AzureNotificationHubsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Azure Notification Hubs push sender as Keyed Service with key "AzureNotificationHubs".
    /// </summary>
    public static IServiceCollection AddGranitNotificationsMobilePushAzureNotificationHubs(
        this IServiceCollection services,
        Action<AzureNotificationHubsOptions>? configure = null)
    {
        services.AddOptions<AzureNotificationHubsOptions>()
            .BindConfiguration(AzureNotificationHubsOptions.SectionName)
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<AzureNotificationHubsOptions>,
            AzureNotificationHubsOptionsValidator>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<IAzureNotificationHubsTransport, AzureNotificationHubsTransport>();
        services.AddKeyedSingleton<IMobilePushSender, AzureNotificationHubsPushSender>("AzureNotificationHubs");

        GranitActivitySourceRegistry.Register(NotificationsMobilePushAnhActivitySource.Name);

        return services;
    }

    /// <summary>
    /// Adds the Azure Notification Hubs health check (tags: <c>readiness</c>, <c>startup</c>).
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Optional check name (default: <c>"azure-notification-hubs"</c>).</param>
    /// <param name="failureStatus">Optional failure status override.</param>
    /// <param name="timeout">Optional timeout override.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHealthChecksBuilder AddGranitAzureNotificationHubsHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "azure-notification-hubs",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null) =>
        builder.Add(new HealthCheckRegistration(
            name,
            sp => new AzureNotificationHubsHealthCheck(
                sp.GetRequiredService<IOptions<AzureNotificationHubsOptions>>()),
            failureStatus,
            ["readiness", "startup"],
            timeout));
}
