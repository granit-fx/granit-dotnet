using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Notifications.MobilePush.AwsSns.Tests;

public sealed class GranitNotificationsMobilePushAwsSnsModuleTests
{
    [Fact]
    public void InheritsGranitModule() =>
        typeof(GranitNotificationsMobilePushAwsSnsModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void IsSealed() =>
        typeof(GranitNotificationsMobilePushAwsSnsModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void ConfigureServices_RegistersMobilePushSender()
    {
        GranitNotificationsMobilePushAwsSnsModule module = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder);

        module.ConfigureServices(context);

        builder.Services.ShouldContain(d =>
            d.IsKeyedService &&
            d.ServiceType == typeof(IMobilePushSender) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }
}
