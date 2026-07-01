using Granit.Notifications.Abstractions;
using Granit.Notifications.WebPush.Internal;
using Granit.Notifications.WebPush.Options;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        // Reader and writer MUST resolve to the SAME instance: the in-memory store keeps its
        // subscriptions in an instance-level dictionary, so the two interfaces are forwarded to a
        // single registration rather than registered against the impl type independently (which
        // would hand out two stores with two dictionaries).
        services.TryAddSingleton<InMemoryWebPushSubscriptionStore>();
        services.TryAddSingleton<IWebPushSubscriptionReader>(sp => sp.GetRequiredService<InMemoryWebPushSubscriptionStore>());
        services.TryAddSingleton<IWebPushSubscriptionWriter>(sp => sp.GetRequiredService<InMemoryWebPushSubscriptionStore>());

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

        // Scoped so the channel can depend on the EF-backed subscription stores (Scoped)
        // registered by AddGranitNotificationsWebPushEntityFrameworkCore without a captive
        // dependency; harmless for the in-memory (Singleton) default store.
        services.AddScoped<INotificationChannel, WebPushNotificationChannel>();

        return services;
    }
}
