using Granit.Modularity;
using Granit.Notifications.Sms.AwsSns.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Sms.AwsSns.Tests;

public sealed class GranitNotificationsSmsAwsSnsModuleTests
{
    [Fact]
    public void InheritsGranitModule() =>
        typeof(GranitNotificationsSmsAwsSnsModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void IsSealed() =>
        typeof(GranitNotificationsSmsAwsSnsModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void ConfigureServices_RegistersSmsSender()
    {
        GranitNotificationsSmsAwsSnsModule module = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder);

        module.ConfigureServices(context);

        builder.Services.ShouldContain(d =>
            d.IsKeyedService &&
            d.ServiceType == typeof(ISmsSender) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }
}
