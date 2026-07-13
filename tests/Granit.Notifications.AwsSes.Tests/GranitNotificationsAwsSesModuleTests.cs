using Granit.Modularity;
using Granit.Notifications.Email;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AwsSes.Tests;

public sealed class GranitNotificationsAwsSesModuleTests
{
    [Fact]
    public void InheritsGranitModule() =>
        typeof(GranitNotificationsAwsSesModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void IsSealed() =>
        typeof(GranitNotificationsAwsSesModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void ConfigureServices_RegistersKeyedEmailSender()
    {
        GranitNotificationsAwsSesModule module = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder);

        module.ConfigureServices(context);

        builder.Services.ShouldContain(d =>
            d.IsKeyedService &&
            d.ServiceType == typeof(IEmailSender) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }
}
