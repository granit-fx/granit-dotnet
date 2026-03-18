using Granit.Core.Modularity;
using Granit.Notifications.Email.AwsSes.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Email.AwsSes.Tests;

public sealed class GranitNotificationsEmailAwsSesModuleTests
{
    [Fact]
    public void InheritsGranitModule() =>
        typeof(GranitNotificationsEmailAwsSesModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void IsSealed() =>
        typeof(GranitNotificationsEmailAwsSesModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void ConfigureServices_RegistersKeyedEmailSender()
    {
        GranitNotificationsEmailAwsSesModule module = new();
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
