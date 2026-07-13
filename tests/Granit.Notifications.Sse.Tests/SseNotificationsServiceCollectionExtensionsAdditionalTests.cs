using Granit.Guids;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Sse.Extensions;
using Granit.Notifications.Sse.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Sse.Tests;

public sealed class SseNotificationsServiceCollectionExtensionsAdditionalTests
{
    [Fact]
    public void AddGranitNotificationsSse_RegistersSseConnectionManager()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IGuidGenerator>(SimpleGuidGenerator.Instance);
        services.AddGranitNotificationsSse();

        using ServiceProvider sp = services.BuildServiceProvider();
        ISseConnectionManager manager = sp.GetRequiredService<ISseConnectionManager>();

        manager.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitNotificationsSse_RegistersNotificationChannel()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IGuidGenerator>(SimpleGuidGenerator.Instance);
        services.AddLogging();
        services.AddGranitNotificationsSse();

        using ServiceProvider sp = services.BuildServiceProvider();
        IEnumerable<INotificationChannel> channels = sp.GetServices<INotificationChannel>();

        channels.ShouldContain(c => c.Name == NotificationChannels.Sse);
    }

    [Fact]
    public void AddGranitNotificationsSse_RegistersOptions()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsSse();

        using ServiceProvider sp = services.BuildServiceProvider();
        IOptions<SseChannelOptions> options = sp.GetRequiredService<IOptions<SseChannelOptions>>();

        options.Value.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitNotificationsSse_WithConfigure_AppliesConfiguration()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsSse(opts => opts.HeartbeatIntervalSeconds = 60);

        using ServiceProvider sp = services.BuildServiceProvider();
        IOptions<SseChannelOptions> options = sp.GetRequiredService<IOptions<SseChannelOptions>>();

        options.Value.HeartbeatIntervalSeconds.ShouldBe(60);
    }

    [Fact]
    public void AddGranitNotificationsSse_NullConfigure_DoesNotThrow()
    {
        ServiceCollection services = new();

        Should.NotThrow(() => services.AddGranitNotificationsSse(configure: null));
    }

    [Fact]
    public void AddGranitNotificationsSse_DefaultHeartbeat_Is30Seconds()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsSse();

        using ServiceProvider sp = services.BuildServiceProvider();
        IOptions<SseChannelOptions> options = sp.GetRequiredService<IOptions<SseChannelOptions>>();

        options.Value.HeartbeatIntervalSeconds.ShouldBe(30);
    }

    [Fact]
    public void AddGranitNotificationsSse_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitNotificationsSse();

        result.ShouldBeSameAs(services);
    }
}
