using Granit.HttpResilience.Extensions;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Zulip.HealthChecks;
using Granit.Notifications.Zulip.Internal;
using Granit.Notifications.Zulip.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.Zulip.Extensions;

/// <summary>Extension methods for the Zulip notification channel.</summary>
public static class ZulipNotificationsServiceCollectionExtensions
{
    /// <summary>Adds the Zulip notification channel with built-in Bot API sender.</summary>
    public static IServiceCollection AddGranitNotificationsZulip(
        this IServiceCollection services,
        Action<ZulipChannelOptions>? configureChannel = null,
        Action<ZulipBotOptions>? configureBot = null)
    {
        services.AddOptions<ZulipChannelOptions>()
            .BindConfiguration(ZulipChannelOptions.SectionName)
            .ValidateOnStart();

        services.AddOptions<ZulipBotOptions>()
            .BindConfiguration(ZulipBotOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configureChannel is not null)
        {
            services.Configure(configureChannel);
        }

        if (configureBot is not null)
        {
            services.Configure(configureBot);
        }

        services.AddGranitHttpClient(ZulipBotSender.HttpClientName, (sp, client) =>
        {
            ZulipBotOptions opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ZulipBotOptions>>().Value;
            client.BaseAddress = new Uri(string.Concat(opts.BaseUrl.TrimEnd('/'), "/"));
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        });

        services.AddSingleton<IZulipSender, ZulipBotSender>();
        services.AddScoped<INotificationChannel, ZulipNotificationChannel>();
        return services;
    }

    /// <summary>
    /// Adds a health check that verifies Zulip Bot API connectivity via <c>GET /api/v1/users/me</c>.
    /// </summary>
    public static IHealthChecksBuilder AddGranitZulipHealthCheck(this IHealthChecksBuilder builder) =>
        builder.Add(new HealthCheckRegistration(
            "zulip",
            sp => new ZulipHealthCheck(
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<IOptions<ZulipBotOptions>>()),
            failureStatus: null,
            tags: ["readiness"]));
}
