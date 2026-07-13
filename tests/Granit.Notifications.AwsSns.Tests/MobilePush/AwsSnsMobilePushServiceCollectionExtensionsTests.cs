using Granit.Notifications.AwsSns.MobilePush.Extensions;
using Granit.Notifications.AwsSns.MobilePush.HealthChecks;
using Granit.Notifications.AwsSns.MobilePush.Internal;
using Granit.Notifications.AwsSns.MobilePush.Options;
using Granit.Notifications.MobilePush;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AwsSns.MobilePush.Tests;

public sealed class AwsSnsMobilePushServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitNotificationsAwsSnsMobilePush_RegistersKeyedPushSender()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAwsSnsMobilePush();

        services.ShouldContain(d =>
            d.IsKeyedService &&
            d.ServiceType == typeof(IMobilePushSender) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitNotificationsAwsSnsMobilePush_RegistersOptionsValidator()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAwsSnsMobilePush();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IValidateOptions<AwsSnsMobilePushOptions>));
    }

    [Fact]
    public void AddGranitNotificationsAwsSnsMobilePush_RegistersTransport()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAwsSnsMobilePush();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IAwsSnsMobilePushTransport) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitNotificationsAwsSnsMobilePush_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitNotificationsAwsSnsMobilePush();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitNotificationsAwsSnsMobilePush_ConfigureDelegateIsApplied()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAwsSnsMobilePush(opts => opts.Region = "us-east-1");

        services.ShouldContain(d =>
            d.ServiceType == typeof(IConfigureOptions<AwsSnsMobilePushOptions>));
    }

    [Fact]
    public void AddGranitNotificationsAwsSnsMobilePush_NullConfigureDoesNotThrow()
    {
        ServiceCollection services = new();
        Should.NotThrow(() => services.AddGranitNotificationsAwsSnsMobilePush(null));
    }

    [Fact]
    public void AddGranitAwsSnsMobilePushHealthCheck_RegistersHealthCheck()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAwsSnsMobilePush();
        services.AddHealthChecks().AddGranitAwsSnsMobilePushHealthCheck();

        services.ShouldContain(d =>
            d.ServiceType == typeof(AwsSnsMobilePushHealthCheck));
    }
}
