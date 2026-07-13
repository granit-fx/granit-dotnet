using Granit.Modularity;
using Granit.Notifications.Email;
using Granit.Notifications.Sms;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AzureCommunicationServices.Tests;

public sealed class GranitNotificationsAzureCommunicationServicesModuleTests
{
    [Fact]
    public void Module_IsAGranitModule() =>
        typeof(GranitNotificationsAzureCommunicationServicesModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitNotificationsAzureCommunicationServicesModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_DependsOnBothChannelModules()
    {
        Type[] depended = typeof(GranitNotificationsAzureCommunicationServicesModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .SelectMany(a => a.DependedTypes)
            .ToArray();

        depended.ShouldContain(typeof(GranitNotificationsEmailModule));
        depended.ShouldContain(typeof(GranitNotificationsSmsModule));
    }

    [Fact]
    public void Module_CanBeInstantiated()
    {
        GranitNotificationsAzureCommunicationServicesModule module = new();
        module.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_NoConfiguration_RegistersNoSender()
    {
        // An email-only (or SMS-only) host must never wire — nor startup-validate —
        // the capability it did not configure.
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        new GranitNotificationsAzureCommunicationServicesModule().ConfigureServices(
            new ServiceConfigurationContext(builder.Services, builder.Configuration, builder));

        builder.Services.ShouldNotContain(d => d.ServiceType == typeof(IEmailSender));
        builder.Services.ShouldNotContain(d => d.ServiceType == typeof(ISmsSender));
    }

    [Fact]
    public void ConfigureServices_EmailSectionOnly_RegistersOnlyTheEmailSender()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["Notifications:AzureCommunicationServices:Email:DefaultSenderEmail"] = "no-reply@test.com";

        new GranitNotificationsAzureCommunicationServicesModule().ConfigureServices(
            new ServiceConfigurationContext(builder.Services, builder.Configuration, builder));

        builder.Services.ShouldContain(d => d.ServiceType == typeof(IEmailSender) && d.IsKeyedService);
        builder.Services.ShouldNotContain(d => d.ServiceType == typeof(ISmsSender));
    }

    [Fact]
    public void ConfigureServices_BothSections_RegistersBothSenders()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["Notifications:AzureCommunicationServices:Email:DefaultSenderEmail"] = "no-reply@test.com";
        builder.Configuration["Notifications:AzureCommunicationServices:Sms:SenderPhoneNumber"] = "+3225551234";

        new GranitNotificationsAzureCommunicationServicesModule().ConfigureServices(
            new ServiceConfigurationContext(builder.Services, builder.Configuration, builder));

        builder.Services.ShouldContain(d => d.ServiceType == typeof(IEmailSender) && d.IsKeyedService);
        builder.Services.ShouldContain(d => d.ServiceType == typeof(ISmsSender) && d.IsKeyedService);
    }
}
