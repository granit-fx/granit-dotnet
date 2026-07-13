using Granit.Diagnostics;
using Granit.Extensions;
using Granit.Http.Resilience.Extensions;
using Granit.Notifications.Email;
using Granit.Notifications.SendGrid.Diagnostics;
using Granit.Notifications.SendGrid.HealthChecks;
using Granit.Notifications.SendGrid.Internal;
using Granit.Notifications.SendGrid.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Granit.Notifications.SendGrid.Extensions;

/// <summary>Extension methods for the SendGrid email provider.</summary>
public static class SendGridEmailServiceCollectionExtensions
{
    private const string ProviderKey = "SendGrid";

    /// <summary>Registers the SendGrid email sender as Keyed Service with key "SendGrid".</summary>
    public static IServiceCollection AddGranitNotificationsSendGrid(
        this IServiceCollection services,
        Action<SendGridEmailOptions>? configure = null)
    {
        services.AddGranitProviderOptions<SendGridEmailOptions>(SendGridEmailOptions.SectionName);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddGranitHttpClient(ProviderKey, (sp, client) =>
        {
            SendGridEmailOptions opts = sp
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<SendGridEmailOptions>>().Value;
            client.BaseAddress = new Uri($"{opts.BaseUrl.TrimEnd('/')}/");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {opts.ApiKey}");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        });

        services.AddSingleton<SendGridEmailSender>();
        services.AddKeyedSingleton<IEmailSender>(
            ProviderKey, (sp, _) => sp.GetRequiredService<SendGridEmailSender>());

        GranitActivitySourceRegistry.Register(NotificationsSendGridActivitySource.Name);

        return services;
    }

    /// <summary>
    /// Adds the SendGrid health check (tags: <c>readiness</c>, <c>startup</c>).
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
            ["readiness", "startup"],
            timeout));
}
