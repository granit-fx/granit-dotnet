using System.Net.Http.Headers;
using System.Text;
using Granit.Http.Resilience.Extensions;
using Granit.Notifications.Sms;
using Granit.Notifications.Twilio.HealthChecks;
using Granit.Notifications.Twilio.Internal;
using Granit.Notifications.Twilio.Options;
using Granit.Notifications.WhatsApp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Granit.Notifications.Twilio.Extensions;

/// <summary>Extension methods for Twilio notification provider registration.</summary>
public static class TwilioNotificationsServiceCollectionExtensions
{
    private const string ProviderKey = "Twilio";

    /// <summary>
    /// Registers the unified Twilio provider as Keyed Services for SMS and WhatsApp.
    /// </summary>
    public static IServiceCollection AddGranitNotificationsTwilio(
        this IServiceCollection services,
        Action<TwilioOptions>? configure = null)
    {
        services.AddOptions<TwilioOptions>()
            .BindConfiguration(TwilioOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddGranitHttpClient(ProviderKey, (sp, client) =>
        {
            TwilioOptions opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TwilioOptions>>().Value;
            client.BaseAddress = new Uri(string.Concat(opts.BaseUrl.TrimEnd('/'), "/"));
            string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{opts.AccountSid}:{opts.AuthToken}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        });

        services.AddSingleton<TwilioNotificationProvider>();
        services.AddKeyedSingleton<ISmsSender>(
            ProviderKey, (sp, _) => sp.GetRequiredService<TwilioNotificationProvider>());
        services.AddKeyedSingleton<IWhatsAppSender>(
            ProviderKey, (sp, _) => sp.GetRequiredService<TwilioNotificationProvider>());

        return services;
    }

    /// <summary>
    /// Adds the Twilio API health check (tags: <c>readiness</c>).
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Optional check name (default: <c>"twilio"</c>).</param>
    /// <param name="failureStatus">Optional failure status override.</param>
    /// <param name="timeout">Optional timeout override.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHealthChecksBuilder AddGranitTwilioHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "twilio",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null) =>
        builder.Add(new HealthCheckRegistration(
            name,
            sp =>
            {
                IHttpClientFactory factory = sp.GetRequiredService<IHttpClientFactory>();
                Microsoft.Extensions.Options.IOptionsMonitor<TwilioOptions> opts =
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<TwilioOptions>>();
                return new TwilioHealthCheck(factory, opts);
            },
            failureStatus,
            ["readiness"],
            timeout));
}
