using Azure.Communication.Sms;
using Azure.Identity;
using Granit.Core.Diagnostics;
using Granit.Notifications.Sms.AzureCommunicationServices.Diagnostics;
using Granit.Notifications.Sms.AzureCommunicationServices.HealthChecks;
using Granit.Notifications.Sms.AzureCommunicationServices.Internal;
using Granit.Notifications.Sms.AzureCommunicationServices.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.Sms.AzureCommunicationServices.Extensions;

/// <summary>Extension methods for the Azure Communication Services SMS provider.</summary>
public static class AcsSmsServiceCollectionExtensions
{
    /// <summary>Registers the ACS SMS sender as Keyed Service with key "AzureCommunicationServices".</summary>
    public static IServiceCollection AddGranitNotificationsSmsAcs(
        this IServiceCollection services,
        Action<AcsSmsOptions>? configure = null)
    {
        services.AddOptions<AcsSmsOptions>()
            .BindConfiguration(AcsSmsOptions.SectionName)
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<AcsSmsOptions>, AcsSmsOptionsValidator>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<IAcsSmsTransport>(sp =>
        {
            AcsSmsOptions opts = sp.GetRequiredService<IOptions<AcsSmsOptions>>().Value;

            SmsClient client = opts.ConnectionString is not null
                ? new SmsClient(opts.ConnectionString)
                : new SmsClient(new Uri(opts.Endpoint!), new DefaultAzureCredential());

            return new AzureAcsSmsTransport(client);
        });

        services.AddKeyedSingleton<ISmsSender, AcsSmsSender>("AzureCommunicationServices");

        GranitActivitySourceRegistry.Register(NotificationsSmsAcsActivitySource.Name);

        return services;
    }

    /// <summary>
    /// Adds the ACS SMS health check (tags: <c>readiness</c>, <c>startup</c>).
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Optional check name (default: <c>"acs-sms"</c>).</param>
    /// <param name="failureStatus">Optional failure status override.</param>
    /// <param name="timeout">Optional timeout override.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHealthChecksBuilder AddGranitAcsSmsHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "acs-sms",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null) =>
        builder.Add(new HealthCheckRegistration(
            name,
            sp => new AcsSmsHealthCheck(
                sp.GetRequiredService<IOptions<AcsSmsOptions>>(),
                sp.GetRequiredService<IAcsSmsTransport>()),
            failureStatus,
            ["readiness", "startup"],
            timeout));
}
