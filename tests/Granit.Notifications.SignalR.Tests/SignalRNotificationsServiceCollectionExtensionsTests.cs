using Granit.Notifications.Abstractions;
using Granit.Notifications.SignalR.Extensions;
using Granit.Notifications.SignalR.Internal;
using Granit.Notifications.SignalR.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.SignalR.Tests;

public sealed class SignalRNotificationsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitNotificationsSignalR_RegistersNotificationChannel()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsSignalR();

        services.ShouldContain(d =>
            d.ServiceType == typeof(INotificationChannel) &&
            d.ImplementationType == typeof(SignalRNotificationChannel) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitNotificationsSignalR_RegistersSignalRChannelOptions()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsSignalR();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IConfigureOptions<SignalRChannelOptions>));
    }

    [Fact]
    public void AddGranitNotificationsSignalR_AppliesConfigureDelegate()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsSignalR(opts => opts.RedisConnectionString = "localhost:6379");

        ServiceProvider sp = services.BuildServiceProvider();
        SignalRChannelOptions options = sp.GetRequiredService<IOptions<SignalRChannelOptions>>().Value;

        options.RedisConnectionString.ShouldBe("localhost:6379");
    }

    [Fact]
    public void AddGranitNotificationsSignalR_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitNotificationsSignalR();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitNotificationsSignalR_WithNullConfigure_DoesNotThrow()
    {
        ServiceCollection services = new();

        Should.NotThrow(() => services.AddGranitNotificationsSignalR(configure: null));
    }

    [Fact]
    public void AddGranitNotificationsSignalR_RegistersSignalRServices()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsSignalR();

        // AddSignalR() registers IHubContext and related services
        services.ShouldContain(d =>
            d.ServiceType.FullName != null &&
            (d.ServiceType.FullName.Contains("SignalR") ||
             d.ServiceType.FullName.Contains("Hub")));
    }

    // -------------------------------------------------------------------------
    // Redis backplane overload
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitNotificationsSignalR_WithRedis_RegistersNotificationChannel()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsSignalR("localhost:6379");

        services.ShouldContain(d =>
            d.ServiceType == typeof(INotificationChannel) &&
            d.ImplementationType == typeof(SignalRNotificationChannel) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitNotificationsSignalR_WithRedis_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitNotificationsSignalR("localhost:6379");

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitNotificationsSignalR_WithRedis_RegistersSignalRChannelOptions()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsSignalR("localhost:6379");

        services.ShouldContain(d =>
            d.ServiceType == typeof(IConfigureOptions<SignalRChannelOptions>));
    }
}
