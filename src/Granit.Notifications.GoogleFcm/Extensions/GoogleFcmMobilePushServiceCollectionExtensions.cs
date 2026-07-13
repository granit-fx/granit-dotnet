using Granit.Diagnostics;
using Granit.Extensions;
using Granit.Http.Resilience.Extensions;
using Granit.Notifications.GoogleFcm.Diagnostics;
using Granit.Notifications.GoogleFcm.HealthChecks;
using Granit.Notifications.GoogleFcm.Internal;
using Granit.Notifications.GoogleFcm.Options;
using Granit.Notifications.MobilePush;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Granit.Notifications.GoogleFcm.Extensions;

/// <summary>Extension methods for the FCM mobile push provider.</summary>
public static class GoogleFcmMobilePushServiceCollectionExtensions
{
    private const string ProviderKey = "GoogleFcm";
    private const string HttpClientName = "GoogleFcmPush";

    /// <summary>Registers the FCM mobile push sender as Keyed Service with key "GoogleFcm".</summary>
    public static IServiceCollection AddGranitNotificationsGoogleFcm(
        this IServiceCollection services,
        Action<GoogleFcmOptions>? configure = null)
    {
        services.AddGranitProviderOptions<GoogleFcmOptions, GoogleFcmOptionsValidator>(GoogleFcmOptions.SectionName);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        // OAuth 2.0 service-account auth for the FCM HTTP v1 API: cached single-flight
        // token provider + Bearer handler (401 ⇒ invalidate, mint fresh, retry once).
        services.TryAddSingleton<IGoogleFcmTokenSource, GoogleApisAuthTokenSource>();
        services.TryAddSingleton<GoogleFcmTokenProvider>();
        services.AddTransient<GoogleFcmAuthenticationHandler>();

        services.AddGranitHttpClient(HttpClientName, (sp, client) =>
        {
            GoogleFcmOptions opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GoogleFcmOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseAddress);
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        }).AddHttpMessageHandler<GoogleFcmAuthenticationHandler>();

        services.AddKeyedSingleton<IMobilePushSender, GoogleFcmMobilePushSender>(ProviderKey);
        GranitActivitySourceRegistry.Register(NotificationsGoogleFcmActivitySource.Name);

        return services;
    }

    /// <summary>
    /// Adds the FCM configuration health check (tags: <c>readiness</c>, <c>startup</c>).
    /// </summary>
    public static IHealthChecksBuilder AddGranitGoogleFcmHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "google-fcm-push",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null)
    {
        builder.Services.AddSingleton<GoogleFcmHealthCheck>();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => sp.GetRequiredService<GoogleFcmHealthCheck>(),
            failureStatus,
            ["readiness", "startup"],
            timeout));
    }
}
