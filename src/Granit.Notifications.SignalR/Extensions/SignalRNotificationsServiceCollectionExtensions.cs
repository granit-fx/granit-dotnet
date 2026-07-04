using Granit.Notifications.Abstractions;
using Granit.Notifications.SignalR.Internal;
using Granit.Notifications.SignalR.Options;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Granit.Notifications.SignalR.Extensions;

/// <summary>
/// Extension methods for registering the SignalR notification channel.
/// </summary>
public static class SignalRNotificationsServiceCollectionExtensions
{
    /// <summary>
    /// Adds the SignalR real-time notification channel with optional Redis backplane.
    /// </summary>
    public static IServiceCollection AddGranitNotificationsSignalR(
        this IServiceCollection services,
        Action<SignalRChannelOptions>? configure = null)
    {
        services.AddOptions<SignalRChannelOptions>()
            .BindConfiguration(SignalRChannelOptions.SectionName)
            .ValidateOnStart();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<INotificationChannel, SignalRNotificationChannel>();

        services.AddSignalR();

        return services;
    }

    /// <summary>
    /// Adds the SignalR real-time notification channel with Redis backplane for Kubernetes.
    /// </summary>
    public static IServiceCollection AddGranitNotificationsSignalR(
        this IServiceCollection services,
        string redisConnectionString)
    {
        services.AddOptions<SignalRChannelOptions>()
            .BindConfiguration(SignalRChannelOptions.SectionName)
            .ValidateOnStart();

        services.AddSingleton<INotificationChannel, SignalRNotificationChannel>();

        services.AddSignalR()
            .AddStackExchangeRedis(redisConnectionString, options => options.Configuration.ChannelPrefix = RedisChannel.Literal("granit-notifications"));

        return services;
    }
}
