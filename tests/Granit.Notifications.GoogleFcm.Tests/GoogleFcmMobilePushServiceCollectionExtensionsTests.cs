using Granit.Notifications.GoogleFcm.Extensions;
using Granit.Notifications.GoogleFcm.Options;
using Granit.Notifications.MobilePush;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.GoogleFcm.Tests;

public sealed class GoogleFcmMobilePushServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitNotificationsGoogleFcm_RegistersKeyedPushSender()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsGoogleFcm();

        services.ShouldContain(d =>
            d.IsKeyedService &&
            d.ServiceType == typeof(IMobilePushSender) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitNotificationsGoogleFcm_RegistersOAuthTokenPipeline()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsGoogleFcm();

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
    public void AddGranitNotificationsGoogleFcm_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddGranitNotificationsGoogleFcm();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitNotificationsGoogleFcm_ConfigureDelegateIsApplied()
    {
        ServiceCollection services = new();
        services.AddGranitNotificationsGoogleFcm(opts => opts.ProjectId = "my-project");

        services.ShouldContain(d =>
            d.ServiceType == typeof(IConfigureOptions<GoogleFcmOptions>));
    }

    [Fact]
    public void AddGranitNotificationsGoogleFcm_NullConfigureDoesNotThrow()
    {
        ServiceCollection services = new();
        Should.NotThrow(() => services.AddGranitNotificationsGoogleFcm(null));
    }
}
