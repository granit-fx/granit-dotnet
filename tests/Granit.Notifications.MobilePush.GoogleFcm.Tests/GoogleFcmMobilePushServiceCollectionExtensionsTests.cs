using Granit.Notifications.MobilePush.GoogleFcm.Extensions;
using Granit.Notifications.MobilePush.GoogleFcm.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.MobilePush.GoogleFcm.Tests;

public sealed class GoogleFcmMobilePushServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitNotificationsMobilePushGoogleFcm_RegistersKeyedPushSender()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsMobilePushGoogleFcm();

        services.ShouldContain(d =>
            d.IsKeyedService &&
            d.ServiceType == typeof(IMobilePushSender) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitNotificationsMobilePushGoogleFcm_RegistersOAuthTokenPipeline()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsMobilePushGoogleFcm();

        services.ShouldContain(d =>
            d.ServiceType == typeof(Internal.IGoogleFcmTokenSource) &&
            d.Lifetime == ServiceLifetime.Singleton);
        services.ShouldContain(d =>
            d.ServiceType == typeof(Internal.GoogleFcmTokenProvider) &&
            d.Lifetime == ServiceLifetime.Singleton);
        services.ShouldContain(d =>
            d.ServiceType == typeof(Internal.GoogleFcmAuthenticationHandler) &&
            d.Lifetime == ServiceLifetime.Transient);
    }

    [Fact]
    public void AddGranitNotificationsMobilePushGoogleFcm_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitNotificationsMobilePushGoogleFcm();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitNotificationsMobilePushGoogleFcm_ConfigureDelegateIsApplied()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsMobilePushGoogleFcm(opts => opts.ProjectId = "my-project");

        services.ShouldContain(d =>
            d.ServiceType == typeof(IConfigureOptions<GoogleFcmOptions>));
    }

    [Fact]
    public void AddGranitNotificationsMobilePushGoogleFcm_NullConfigureDoesNotThrow()
    {
        ServiceCollection services = new();
        Should.NotThrow(() => services.AddGranitNotificationsMobilePushGoogleFcm(null));
    }
}
