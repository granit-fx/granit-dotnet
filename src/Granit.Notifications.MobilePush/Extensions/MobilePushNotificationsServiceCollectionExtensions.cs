using Granit.Notifications.Abstractions;
using Granit.Notifications.MobilePush.Internal;
using Granit.Notifications.MobilePush.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Notifications.MobilePush.Extensions;

/// <summary>Extension methods for the mobile push notification channel.</summary>
public static class MobilePushNotificationsServiceCollectionExtensions
{
    /// <summary>Adds the mobile push notification channel with Keyed Services provider resolution.</summary>
    public static IServiceCollection AddGranitNotificationsMobilePush(
        this IServiceCollection services,
        Action<MobilePushChannelOptions>? configure = null)
    {
        services.AddOptions<MobilePushChannelOptions>()
            .BindConfiguration(MobilePushChannelOptions.SectionName)
            .ValidateOnStart();

        services.AddOptions<MobilePushTokenHasherOptions>()
            .BindConfiguration(MobilePushTokenHasherOptions.SectionName);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<IMobilePushTokenHasher, HmacMobilePushTokenHasher>();
        services.TryAddSingleton<IMobilePushTokenReader, InMemoryMobilePushTokenStore>();
        services.TryAddSingleton<IMobilePushTokenWriter, InMemoryMobilePushTokenStore>();
        services.TryAddScoped<IMobilePushEventPublisher, NullMobilePushEventPublisher>();
        services.AddScoped<INotificationChannel, MobilePushNotificationChannel>();
        return services;
    }
}
