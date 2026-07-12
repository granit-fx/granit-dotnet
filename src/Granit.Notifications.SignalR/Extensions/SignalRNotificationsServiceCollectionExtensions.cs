using Granit.Notifications.Abstractions;
using Granit.Notifications.SignalR.Internal;
using Granit.Notifications.SignalR.Options;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
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
    /// <remarks>
    /// The Redis backplane must be wired at registration time, so
    /// <see cref="SignalRChannelOptions.RedisConnectionString"/> is only honored when supplied
    /// through the <paramref name="configure"/> delegate here. To source it from
    /// <c>appsettings.json</c> (<c>Notifications:SignalR:RedisConnectionString</c>), use the
    /// <see cref="AddGranitNotificationsSignalR(IServiceCollection, IConfiguration, Action{SignalRChannelOptions}?)"/>
    /// overload instead.
    /// </remarks>
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

        // Probe the delegate so a connection string supplied through it wires the
        // backplane instead of being silently ignored (delegates must stay idempotent).
        SignalRChannelOptions probe = new();
        configure?.Invoke(probe);

        AddSignalRCore(services, probe.RedisConnectionString);

        return services;
    }

    /// <summary>
    /// Adds the SignalR real-time notification channel, wiring the Redis backplane from
    /// configuration (<c>Notifications:SignalR:RedisConnectionString</c>) when present.
    /// </summary>
    public static IServiceCollection AddGranitNotificationsSignalR(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<SignalRChannelOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<SignalRChannelOptions>()
            .BindConfiguration(SignalRChannelOptions.SectionName)
            .ValidateOnStart();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<INotificationChannel, SignalRNotificationChannel>();

        SignalRChannelOptions probe = new();
        configuration.GetSection(SignalRChannelOptions.SectionName).Bind(probe);
        configure?.Invoke(probe);

        AddSignalRCore(services, probe.RedisConnectionString);

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

        AddSignalRCore(services, redisConnectionString);

        return services;
    }

    private static void AddSignalRCore(IServiceCollection services, string? redisConnectionString)
    {
        ISignalRServerBuilder signalR = services.AddSignalR();

        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            signalR.AddStackExchangeRedis(
                redisConnectionString,
                options => options.Configuration.ChannelPrefix = RedisChannel.Literal("granit-notifications"));
        }
    }
}
