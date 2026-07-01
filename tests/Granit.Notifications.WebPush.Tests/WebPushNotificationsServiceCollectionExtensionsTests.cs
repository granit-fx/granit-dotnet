using Granit.Notifications.WebPush.Extensions;
using Granit.Notifications.WebPush.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.WebPush.Tests;

public sealed class WebPushNotificationsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitNotificationsWebPush_RegistersOptions()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsWebPush();

        using ServiceProvider sp = services.BuildServiceProvider();
        IOptions<WebPushChannelOptions> options = sp.GetRequiredService<IOptions<WebPushChannelOptions>>();

        options.Value.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitNotificationsWebPush_RegistersPushSubscriptionReader()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsWebPush();

        using ServiceProvider sp = services.BuildServiceProvider();
        IWebPushSubscriptionReader reader = sp.GetRequiredService<IWebPushSubscriptionReader>();

        reader.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitNotificationsWebPush_RegistersPushSubscriptionWriter()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsWebPush();

        using ServiceProvider sp = services.BuildServiceProvider();
        IWebPushSubscriptionWriter writer = sp.GetRequiredService<IWebPushSubscriptionWriter>();

        writer.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitNotificationsWebPush_WithConfigure_AppliesConfiguration()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsWebPush(opts =>
        {
            opts.VapidSubject = "mailto:test@example.com";
            opts.VapidPublicKey = "test-public-key";
            opts.VapidPrivateKey = "test-private-key";
        });

        using ServiceProvider sp = services.BuildServiceProvider();
        IOptions<WebPushChannelOptions> options = sp.GetRequiredService<IOptions<WebPushChannelOptions>>();

        options.Value.VapidSubject.ShouldBe("mailto:test@example.com");
    }

    [Fact]
    public void AddGranitNotificationsWebPush_NullConfigure_DoesNotThrow()
    {
        ServiceCollection services = new();

        Should.NotThrow(() => services.AddGranitNotificationsWebPush(configure: null));
    }

    [Fact]
    public void AddGranitNotificationsWebPush_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitNotificationsWebPush();

        result.ShouldBeSameAs(services);
    }
}
