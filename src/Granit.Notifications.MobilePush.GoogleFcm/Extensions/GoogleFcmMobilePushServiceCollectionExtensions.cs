using Granit.Http.Resilience.Extensions;
using Granit.Notifications.MobilePush.GoogleFcm.Internal;
using Granit.Notifications.MobilePush.GoogleFcm.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Notifications.MobilePush.GoogleFcm.Extensions;

/// <summary>Extension methods for the FCM mobile push provider.</summary>
public static class GoogleFcmMobilePushServiceCollectionExtensions
{
    private const string ProviderKey = "GoogleFcm";
    private const string HttpClientName = "GoogleFcmPush";

    /// <summary>Registers the FCM mobile push sender as Keyed Service with key "GoogleFcm".</summary>
    public static IServiceCollection AddGranitNotificationsMobilePushGoogleFcm(
        this IServiceCollection services,
        Action<GoogleFcmOptions>? configure = null)
    {
        services.AddOptions<GoogleFcmOptions>()
            .BindConfiguration(GoogleFcmOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddGranitHttpClient(HttpClientName, (sp, client) =>
        {
            GoogleFcmOptions opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GoogleFcmOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseAddress);
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        });

        services.AddKeyedSingleton<IMobilePushSender, GoogleFcmMobilePushSender>(ProviderKey);
        return services;
    }
}
