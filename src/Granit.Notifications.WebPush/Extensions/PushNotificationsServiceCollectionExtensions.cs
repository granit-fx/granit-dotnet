using Granit.Notifications.Abstractions;
using Granit.Notifications.WebPush.Internal;
using Granit.Notifications.WebPush.Options;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.WebPush.Extensions;

/// <summary>Extension methods for Web Push notification channel registration.</summary>
public static class PushNotificationsServiceCollectionExtensions
{
    /// <summary>Registers the W3C Web Push (VAPID) notification channel.</summary>
    public static IServiceCollection AddGranitNotificationsWebPush(
        this IServiceCollection services,
        Action<PushChannelOptions>? configure = null)
    {
        services.AddOptions<PushChannelOptions>()
            .BindConfiguration(PushChannelOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<InMemoryPushSubscriptionStore>();
        services.AddSingleton<IPushSubscriptionReader>(sp => sp.GetRequiredService<InMemoryPushSubscriptionStore>());
        services.AddSingleton<IPushSubscriptionWriter>(sp => sp.GetRequiredService<InMemoryPushSubscriptionStore>());

        services.AddSingleton(sp =>
        {
            PushChannelOptions opts = sp.GetRequiredService<IOptions<PushChannelOptions>>().Value;
            PushServiceClient client = new();
            client.DefaultAuthentication = new VapidAuthentication(
                opts.VapidPublicKey, opts.VapidPrivateKey)
            {
                Subject = opts.VapidSubject,
            };
            return client;
        });

        services.AddSingleton<INotificationChannel, PushNotificationChannel>();

        return services;
    }
}
