using Granit.Notifications.AwsSns.Sms.Extensions;
using Granit.Notifications.AwsSns.Sms.HealthChecks;
using Granit.Notifications.AwsSns.Sms.Internal;
using Granit.Notifications.AwsSns.Sms.Options;
using Granit.Notifications.Sms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AwsSns.Sms.Tests;

public sealed class SnsSmsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitNotificationsAwsSnsSms_RegistersKeyedSmsSender()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAwsSnsSms();

        services.ShouldContain(d =>
            d.IsKeyedService &&
            d.ServiceType == typeof(ISmsSender) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitNotificationsAwsSnsSms_RegistersOptionsValidator()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAwsSnsSms();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IValidateOptions<AwsSnsSmsOptions>));
    }

    [Fact]
    public void AddGranitNotificationsAwsSnsSms_RegistersTransport()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAwsSnsSms();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IAwsSnsSmsTransport) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitNotificationsAwsSnsSms_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitNotificationsAwsSnsSms();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitNotificationsAwsSnsSms_ConfigureDelegateIsApplied()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAwsSnsSms(opts => opts.Region = "us-east-1");

        services.ShouldContain(d =>
            d.ServiceType == typeof(IConfigureOptions<AwsSnsSmsOptions>));
    }

    [Fact]
    public void AddGranitNotificationsAwsSnsSms_NullConfigureDoesNotThrow()
    {
        ServiceCollection services = new();
        Should.NotThrow(() => services.AddGranitNotificationsAwsSnsSms(null));
    }

    [Fact]
    public void AddGranitAwsSnsSmsHealthCheck_RegistersHealthCheck()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAwsSnsSms();
        services.AddHealthChecks().AddGranitAwsSnsSmsHealthCheck();

        services.ShouldContain(d =>
            d.ServiceType == typeof(AwsSnsSmsHealthCheck));
    }
}
