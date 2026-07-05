using Granit.Notifications.Abstractions;
using Granit.Notifications.Sse.Extensions;
using Granit.Notifications.Sse.Internal;
using Granit.Notifications.Sse.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Sse.Tests;

public sealed class SseNotificationsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitNotificationsSse_RegistersNotificationChannel()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsSse();

        services.ShouldContain(d =>
            d.ServiceType == typeof(INotificationChannel) &&
            d.ImplementationType == typeof(SseNotificationChannel) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitNotificationsSse_RegistersConnectionManager()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsSse();

        services.ShouldContain(d =>
            d.ServiceType == typeof(ISseConnectionManager) &&
            d.ImplementationType == typeof(SseConnectionManager) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitNotificationsSse_RegistersSseChannelOptions()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsSse();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IConfigureOptions<SseChannelOptions>));
    }

    [Fact]
    public void AddGranitNotificationsSse_AppliesConfigureDelegate()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsSse(opts => opts.HeartbeatIntervalSeconds = 15);

        ServiceProvider sp = services.BuildServiceProvider();
        SseChannelOptions options = sp.GetRequiredService<IOptions<SseChannelOptions>>().Value;

        options.HeartbeatIntervalSeconds.ShouldBe(15);
    }

    [Fact]
    public void AddGranitNotificationsSse_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitNotificationsSse();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitNotificationsSse_WithNullConfigure_DoesNotThrow()
    {
        ServiceCollection services = new();

        Should.NotThrow(() => services.AddGranitNotificationsSse(configure: null));
    }
}
