using Granit.Notifications.Abstractions;
using Granit.Notifications.MobilePush.Internal;
using Granit.Notifications.MobilePush.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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
        services.TryAddSingleton<IValidateOptions<MobilePushChannelOptions>, MobilePushChannelOptionsValidator>();

        services.AddOptions<MobilePushTokenHasherOptions>()
            .BindConfiguration(MobilePushTokenHasherOptions.SectionName);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<IMobilePushTokenHasher, HmacMobilePushTokenHasher>();

        // Reader and writer MUST resolve to the SAME instance: the in-memory store keeps its
        // tokens in an instance-level dictionary, so registering the two interfaces against the
        // impl type independently would hand out two stores with two dictionaries — a token
        // written through IMobilePushTokenWriter would never be visible through IMobilePushTokenReader.
        services.TryAddSingleton<InMemoryMobilePushTokenStore>();
        services.TryAddSingleton<IMobilePushTokenReader>(sp => sp.GetRequiredService<InMemoryMobilePushTokenStore>());
        services.TryAddSingleton<IMobilePushTokenWriter>(sp => sp.GetRequiredService<InMemoryMobilePushTokenStore>());
        services.TryAddScoped<IMobilePushEventPublisher, NullMobilePushEventPublisher>();
        services.AddScoped<INotificationChannel, MobilePushNotificationChannel>();
        return services;
    }
}
