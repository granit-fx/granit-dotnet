using Granit.Notifications.Abstractions;
using Granit.Notifications.SignalR.Extensions;
using Granit.Notifications.SignalR.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.SignalR.Tests;

public sealed class SignalRNotificationsServiceCollectionExtensionsAdditionalTests
{
    [Fact]
    public void AddGranitNotificationsSignalR_RegistersOptions()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsSignalR();

        using ServiceProvider sp = services.BuildServiceProvider();
        IOptions<SignalRChannelOptions> options = sp.GetRequiredService<IOptions<SignalRChannelOptions>>();

        options.Value.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitNotificationsSignalR_RegistersNotificationChannel()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsSignalR();

        services.ShouldContain(d =>
            d.ServiceType == typeof(INotificationChannel) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitNotificationsSignalR_WithConfigure_AppliesConfiguration()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsSignalR(opts => opts.RedisConnectionString = "localhost:6379");

        using ServiceProvider sp = services.BuildServiceProvider();
        IOptions<SignalRChannelOptions> options = sp.GetRequiredService<IOptions<SignalRChannelOptions>>();

        options.Value.RedisConnectionString.ShouldBe("localhost:6379");
    }

    [Fact]
    public void AddGranitNotificationsSignalR_NullConfigure_DoesNotThrow()
    {
        ServiceCollection services = new();

        Should.NotThrow(() => services.AddGranitNotificationsSignalR(configure: null));
    }

    [Fact]
    public void AddGranitNotificationsSignalR_DefaultOptions_RedisConnectionStringIsNull()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsSignalR();

        using ServiceProvider sp = services.BuildServiceProvider();
        IOptions<SignalRChannelOptions> options = sp.GetRequiredService<IOptions<SignalRChannelOptions>>();

        options.Value.RedisConnectionString.ShouldBeNull();
    }

    [Fact]
    public void AddGranitNotificationsSignalR_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitNotificationsSignalR();

        result.ShouldBeSameAs(services);
    }
}
