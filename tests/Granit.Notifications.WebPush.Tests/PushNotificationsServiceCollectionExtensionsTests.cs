using Granit.Notifications.WebPush;
using Granit.Notifications.WebPush.Extensions;
using Granit.Notifications.WebPush.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.WebPush.Tests;

public sealed class PushNotificationsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitNotificationsPush_RegistersOptions()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsPush();

        using ServiceProvider sp = services.BuildServiceProvider();
        IOptions<PushChannelOptions> options = sp.GetRequiredService<IOptions<PushChannelOptions>>();

        options.Value.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitNotificationsPush_RegistersPushSubscriptionReader()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsPush();

        using ServiceProvider sp = services.BuildServiceProvider();
        IPushSubscriptionReader reader = sp.GetRequiredService<IPushSubscriptionReader>();

        reader.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitNotificationsPush_RegistersPushSubscriptionWriter()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsPush();

        using ServiceProvider sp = services.BuildServiceProvider();
        IPushSubscriptionWriter writer = sp.GetRequiredService<IPushSubscriptionWriter>();

        writer.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitNotificationsPush_WithConfigure_AppliesConfiguration()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsPush(opts =>
        {
            opts.VapidSubject = "mailto:test@example.com";
            opts.VapidPublicKey = "test-public-key";
            opts.VapidPrivateKey = "test-private-key";
        });

        using ServiceProvider sp = services.BuildServiceProvider();
        IOptions<PushChannelOptions> options = sp.GetRequiredService<IOptions<PushChannelOptions>>();

        options.Value.VapidSubject.ShouldBe("mailto:test@example.com");
    }

    [Fact]
    public void AddGranitNotificationsPush_NullConfigure_DoesNotThrow()
    {
        ServiceCollection services = new();

        Should.NotThrow(() => services.AddGranitNotificationsPush(configure: null));
    }

    [Fact]
    public void AddGranitNotificationsPush_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitNotificationsPush();

        result.ShouldBeSameAs(services);
    }
}
