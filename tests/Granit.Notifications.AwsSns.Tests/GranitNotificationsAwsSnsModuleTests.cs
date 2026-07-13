using Granit.Modularity;
using Granit.Notifications.MobilePush;
using Granit.Notifications.Sms;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AwsSns.Tests;

public sealed class GranitNotificationsAwsSnsModuleTests
{
    [Fact]
    public void Module_IsAGranitModule() =>
        typeof(GranitNotificationsAwsSnsModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitNotificationsAwsSnsModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_DependsOnBothChannelModules()
    {
        Type[] depended = typeof(GranitNotificationsAwsSnsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .SelectMany(a => a.DependedTypes)
            .ToArray();

        depended.ShouldContain(typeof(GranitNotificationsMobilePushModule));
        depended.ShouldContain(typeof(GranitNotificationsSmsModule));
    }

    [Fact]
    public void Module_CanBeInstantiated()
    {
        GranitNotificationsAwsSnsModule module = new();
        module.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_NoConfiguration_RegistersNoSender()
    {
        // An SMS-only (or push-only) host must never wire — nor startup-validate —
        // the capability it did not configure.
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        new GranitNotificationsAwsSnsModule().ConfigureServices(
            new ServiceConfigurationContext(builder.Services, builder.Configuration, builder));

        builder.Services.ShouldNotContain(d => d.ServiceType == typeof(ISmsSender));
        builder.Services.ShouldNotContain(d => d.ServiceType == typeof(IMobilePushSender));
    }

    [Fact]
    public void ConfigureServices_SmsSectionOnly_RegistersOnlyTheSmsSender()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["Notifications:AwsSns:Sms:Region"] = "eu-west-1";

        new GranitNotificationsAwsSnsModule().ConfigureServices(
            new ServiceConfigurationContext(builder.Services, builder.Configuration, builder));

        builder.Services.ShouldContain(d => d.ServiceType == typeof(ISmsSender) && d.IsKeyedService);
        builder.Services.ShouldNotContain(d => d.ServiceType == typeof(IMobilePushSender));
    }

    [Fact]
    public void ConfigureServices_BothSections_RegistersBothSenders()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["Notifications:AwsSns:Sms:Region"] = "eu-west-1";
        builder.Configuration["Notifications:AwsSns:MobilePush:Region"] = "eu-west-1";

        new GranitNotificationsAwsSnsModule().ConfigureServices(
            new ServiceConfigurationContext(builder.Services, builder.Configuration, builder));

        builder.Services.ShouldContain(d => d.ServiceType == typeof(ISmsSender) && d.IsKeyedService);
        builder.Services.ShouldContain(d => d.ServiceType == typeof(IMobilePushSender) && d.IsKeyedService);
    }
}
