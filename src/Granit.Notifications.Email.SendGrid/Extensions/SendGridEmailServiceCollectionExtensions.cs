using Granit.Diagnostics;
using Granit.Http.Resilience.Extensions;
using Granit.Notifications.Email.SendGrid.Diagnostics;
using Granit.Notifications.Email.SendGrid.HealthChecks;
using Granit.Notifications.Email.SendGrid.Internal;
using Granit.Notifications.Email.SendGrid.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Granit.Notifications.Email.SendGrid.Extensions;

/// <summary>Extension methods for the SendGrid email provider.</summary>
public static class SendGridEmailServiceCollectionExtensions
{
    private const string ProviderKey = "SendGrid";

    /// <summary>Registers the SendGrid email sender as Keyed Service with key "SendGrid".</summary>
    public static IServiceCollection AddGranitNotificationsEmailSendGrid(
        this IServiceCollection services,
        Action<SendGridEmailOptions>? configure = null)
    {
        services.AddOptions<SendGridEmailOptions>()
            .BindConfiguration(SendGridEmailOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddGranitHttpClient(ProviderKey, (sp, client) =>
        {
            SendGridEmailOptions opts = sp
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<SendGridEmailOptions>>().Value;
            client.BaseAddress = new Uri(string.Concat(opts.BaseUrl.TrimEnd('/'), "/"));
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {opts.ApiKey}");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        });

        services.AddSingleton<SendGridEmailSender>();
        services.AddKeyedSingleton<IEmailSender>(
            ProviderKey, (sp, _) => sp.GetRequiredService<SendGridEmailSender>());

        GranitActivitySourceRegistry.Register(NotificationsEmailSendGridActivitySource.Name);

        return services;
    }

    /// <summary>
    /// Adds the SendGrid health check (tags: <c>readiness</c>).
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Optional check name (default: <c>"sendgrid"</c>).</param>
    /// <param name="failureStatus">Optional failure status override.</param>
    /// <param name="timeout">Optional timeout override.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHealthChecksBuilder AddGranitSendGridHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "sendgrid",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null) =>
        builder.Add(new HealthCheckRegistration(
            name,
            sp => new SendGridHealthCheck(sp.GetRequiredService<IHttpClientFactory>()),
            failureStatus,
            ["readiness"],
            timeout));
}
