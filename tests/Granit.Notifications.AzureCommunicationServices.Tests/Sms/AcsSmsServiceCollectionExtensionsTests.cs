using Granit.Notifications.AzureCommunicationServices.Sms.Extensions;
using Granit.Notifications.AzureCommunicationServices.Sms.Internal;
using Granit.Notifications.AzureCommunicationServices.Sms.Options;
using Granit.Notifications.Sms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AzureCommunicationServices.Sms.Tests;

public sealed class AcsSmsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitNotificationsAcsSms_RegistersKeyedSmsSender()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAcsSms();

        services.ShouldContain(d =>
            d.IsKeyedService &&
            d.ServiceType == typeof(ISmsSender) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitNotificationsAcsSms_RegistersAcsSmsOptionsValidator()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAcsSms();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IValidateOptions<AcsSmsOptions>));
    }

    [Fact]
    public void AddGranitNotificationsAcsSms_AppliesConfigureDelegate()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitNotificationsAcsSms(opts =>
        {
            opts.ConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=dGVzdA==";
            opts.FromPhoneNumber = "+15551234567";
        });

        ServiceProvider sp = services.BuildServiceProvider();
        AcsSmsOptions options = sp.GetRequiredService<IOptions<AcsSmsOptions>>().Value;

        options.ConnectionString.ShouldNotBeNull();
        options.FromPhoneNumber.ShouldBe("+15551234567");
    }

    [Fact]
    public void AddGranitNotificationsAcsSms_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitNotificationsAcsSms();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitNotificationsAcsSms_WithNullConfigure_DoesNotThrow()
    {
        ServiceCollection services = new();

        Should.NotThrow(() => services.AddGranitNotificationsAcsSms(configure: null));
    }

    [Fact]
    public void AddGranitNotificationsAcsSms_RegistersTransport()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsAcsSms();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IAcsSmsTransport) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }
}
