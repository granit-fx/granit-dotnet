using Granit.Notifications.Abstractions;
using Granit.Notifications.Sms.Extensions;
using Granit.Notifications.Sms.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Sms.Tests;

public sealed class SmsNotificationsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitNotificationsSms_RegistersNotificationChannel()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsSms();

        services.ShouldContain(d =>
            d.ServiceType == typeof(INotificationChannel) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitNotificationsSms_RegistersOptions()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Notifications:Sms:Provider"] = "Twilio",
        }).Build());
        services.AddGranitNotificationsSms();

        // Satisfies the keyed-provider startup validator — these tests assert options mechanics.
        services.AddKeyedSingleton("Twilio", (_, _) => Substitute.For<ISmsSender>());
        using ServiceProvider sp = services.BuildServiceProvider();
        IOptions<SmsChannelOptions> options = sp.GetRequiredService<IOptions<SmsChannelOptions>>();

        options.Value.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitNotificationsSms_WithConfigure_AppliesConfiguration()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsSms(opts =>
        {
            opts.Provider = "Twilio";
            opts.SenderId = "CustomSender";
        });

        // Satisfies the keyed-provider startup validator — these tests assert options mechanics.
        services.AddKeyedSingleton("Twilio", (_, _) => Substitute.For<ISmsSender>());
        using ServiceProvider sp = services.BuildServiceProvider();
        IOptions<SmsChannelOptions> options = sp.GetRequiredService<IOptions<SmsChannelOptions>>();

        options.Value.Provider.ShouldBe("Twilio");
        options.Value.SenderId.ShouldBe("CustomSender");
    }

    [Fact]
    public void AddGranitNotificationsSms_NullConfigure_DoesNotThrow()
    {
        ServiceCollection services = new();

        Should.NotThrow(() => services.AddGranitNotificationsSms(configure: null));
    }

    [Fact]
    public void AddGranitNotificationsSms_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitNotificationsSms();

        result.ShouldBeSameAs(services);
    }
}
