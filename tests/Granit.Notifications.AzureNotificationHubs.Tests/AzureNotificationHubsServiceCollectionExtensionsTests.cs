// =============================================================================
// Tests - AzureNotificationHubsServiceCollectionExtensions
// =============================================================================
// Verifies DI registration: keyed service, options, validator, transport.
// =============================================================================

using Granit.Notifications.AzureNotificationHubs.Extensions;
using Granit.Notifications.AzureNotificationHubs.Internal;
using Granit.Notifications.AzureNotificationHubs.Options;
using Granit.Notifications.MobilePush;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AzureNotificationHubs.Tests;

public sealed class AzureNotificationHubsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranit_RegistersKeyedMobilePushSender()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAzureNotificationHubs();

        services.ShouldContain(d =>
            d.IsKeyedService &&
            d.ServiceType == typeof(IMobilePushSender) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranit_RegistersOptions()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAzureNotificationHubs();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IConfigureOptions<AzureNotificationHubsOptions>));
    }

    [Fact]
    public void AddGranit_RegistersOptionsValidator()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAzureNotificationHubs();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IValidateOptions<AzureNotificationHubsOptions>) &&
            d.ImplementationType == typeof(AzureNotificationHubsOptionsValidator));
    }

    [Fact]
    public void AddGranit_RegistersTransport()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAzureNotificationHubs();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IAzureNotificationHubsTransport) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranit_AppliesConfigureDelegate()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsAzureNotificationHubs(opts =>
        {
            opts.ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=Test;SharedAccessKey=abc=";
            opts.HubName = "test-hub";
            opts.TimeoutSeconds = 60;
        });

        ServiceProvider sp = services.BuildServiceProvider();
        AzureNotificationHubsOptions options =
            sp.GetRequiredService<IOptions<AzureNotificationHubsOptions>>().Value;

        options.ConnectionString.ShouldContain("test.servicebus.windows.net");
        options.HubName.ShouldBe("test-hub");
        options.TimeoutSeconds.ShouldBe(60);
    }

    [Fact]
    public void AddGranit_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitNotificationsAzureNotificationHubs();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranit_WithNullConfigure_DoesNotThrow()
    {
        ServiceCollection services = new();

        Should.NotThrow(() =>
            services.AddGranitNotificationsAzureNotificationHubs(configure: null));
    }
}
