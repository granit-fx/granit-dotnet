using Granit.Notifications.Abstractions;
using Granit.Notifications.WebPush.Internal;
using Granit.Notifications.WebPush.Options;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.WebPush.Extensions;

/// <summary>Extension methods for Web Push notification channel registration.</summary>
public static class WebPushNotificationsServiceCollectionExtensions
{
    /// <summary>Registers the W3C Web Push (VAPID) notification channel.</summary>
    public static IServiceCollection AddGranitNotificationsWebPush(
        this IServiceCollection services,
        Action<WebPushChannelOptions>? configure = null)
    {
        services.AddOptions<WebPushChannelOptions>()
            .BindConfiguration(WebPushChannelOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<InMemoryWebPushSubscriptionStore>();
        services.AddSingleton<IWebPushSubscriptionReader>(sp => sp.GetRequiredService<InMemoryWebPushSubscriptionStore>());
        services.AddSingleton<IWebPushSubscriptionWriter>(sp => sp.GetRequiredService<InMemoryWebPushSubscriptionStore>());

        services.AddSingleton(sp =>
        {
            WebPushChannelOptions opts = sp.GetRequiredService<IOptions<WebPushChannelOptions>>().Value;
            PushServiceClient client = new();
            client.DefaultAuthentication = new VapidAuthentication(
                opts.VapidPublicKey, opts.VapidPrivateKey)
            {
                Subject = opts.VapidSubject,
            };
            return client;
        });

        services.AddSingleton<INotificationChannel, WebPushNotificationChannel>();

        return services;
    }
}
