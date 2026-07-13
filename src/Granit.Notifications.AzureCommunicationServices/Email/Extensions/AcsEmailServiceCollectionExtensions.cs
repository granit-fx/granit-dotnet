using Azure.Communication.Email;
using Azure.Identity;
using Granit.Diagnostics;
using Granit.Extensions;
using Granit.Notifications.AzureCommunicationServices.Email.Diagnostics;
using Granit.Notifications.AzureCommunicationServices.Email.HealthChecks;
using Granit.Notifications.AzureCommunicationServices.Email.Internal;
using Granit.Notifications.AzureCommunicationServices.Email.Options;
using Granit.Notifications.Email;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.AzureCommunicationServices.Email.Extensions;

/// <summary>Extension methods for the Azure Communication Services email provider.</summary>
public static class AcsEmailServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Azure Communication Services email sender as Keyed Service
    /// with key "AzureCommunicationServices".
    /// </summary>
    public static IServiceCollection AddGranitNotificationsAcsEmail(
        this IServiceCollection services,
        Action<AcsEmailOptions>? configure = null)
    {
        services.AddGranitProviderOptions<AcsEmailOptions, AcsEmailOptionsValidator>(AcsEmailOptions.SectionName);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<EmailClient>(sp =>
        {
            AcsEmailOptions opts = sp.GetRequiredService<IOptions<AcsEmailOptions>>().Value;

            return opts.ConnectionString is not null
                ? new EmailClient(opts.ConnectionString)
                : new EmailClient(new Uri(opts.Endpoint!), new DefaultAzureCredential());
        });

        services.AddSingleton<IAcsEmailTransport, AzureAcsEmailTransport>();

        services.AddKeyedSingleton<IEmailSender, AcsEmailSender>("AzureCommunicationServices");

        GranitActivitySourceRegistry.Register(NotificationsAcsEmailActivitySource.Name);

        return services;
    }

    /// <summary>
    /// Adds the Azure Communication Services email health check
    /// (tags: <c>readiness</c>, <c>startup</c>).
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Optional check name (default: <c>"acs-email"</c>).</param>
    /// <param name="failureStatus">Optional failure status override.</param>
    /// <param name="timeout">Optional timeout override.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHealthChecksBuilder AddGranitAcsEmailHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "acs-email",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null) =>
        builder.Add(new HealthCheckRegistration(
            name,
            sp => new AcsEmailHealthCheck(sp.GetRequiredService<IOptions<AcsEmailOptions>>()),
            failureStatus,
            ["readiness", "startup"],
            timeout));
}
