using Granit.Notifications.Abstractions;
using Granit.Notifications.Diagnostics;
using Granit.Notifications.Extensions;
using Granit.Notifications.Handlers;
using Granit.Notifications.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class NotificationsHostApplicationBuilderExtensionsTests
{
    [Fact]
    public void AddGranitNotifications_RegistersUserNotificationReader()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitNotifications();

        builder.Services.ShouldContain(d => d.ServiceType == typeof(IUserNotificationReader));
    }

    [Fact]
    public void AddGranitNotifications_RegistersUserNotificationWriter()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitNotifications();

        builder.Services.ShouldContain(d => d.ServiceType == typeof(IUserNotificationWriter));
    }

    [Fact]
    public void AddGranitNotifications_RegistersNotificationPreferenceReader()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitNotifications();

        builder.Services.ShouldContain(d => d.ServiceType == typeof(INotificationPreferenceReader));
    }

    [Fact]
    public void AddGranitNotifications_RegistersNotificationPreferenceWriter()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitNotifications();

        builder.Services.ShouldContain(d => d.ServiceType == typeof(INotificationPreferenceWriter));
    }

    [Fact]
    public void AddGranitNotifications_RegistersNotificationSubscriptionReader()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitNotifications();

        builder.Services.ShouldContain(d => d.ServiceType == typeof(INotificationSubscriptionReader));
    }

    [Fact]
    public void AddGranitNotifications_RegistersNotificationSubscriptionWriter()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitNotifications();

        builder.Services.ShouldContain(d => d.ServiceType == typeof(INotificationSubscriptionWriter));
    }

    [Fact]
    public void AddGranitNotifications_RegistersNotificationDeliveryWriter()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitNotifications();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(INotificationDeliveryWriter) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitNotifications_RegistersNotificationDefinitionStore()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitNotifications();

        builder.Services.ShouldContain(d => d.ServiceType == typeof(INotificationDefinitionStore));
    }

    [Fact]
    public void AddGranitNotifications_RegistersNotificationPublisher()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitNotifications();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(INotificationPublisher) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitNotifications_RegistersNotificationsMetrics()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitNotifications();
        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        NotificationsMetrics metrics = sp.GetRequiredService<NotificationsMetrics>();

        metrics.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitNotifications_RegistersNotificationsOptions()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitNotifications();
        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        IOptions<NotificationsOptions> options = sp.GetRequiredService<IOptions<NotificationsOptions>>();

        options.Value.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitNotifications_RegistersInAppChannel()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitNotifications();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(INotificationChannel) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitNotifications_RegistersFanoutHandler()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitNotifications();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(NotificationFanoutHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitNotifications_RegistersDeliveryHandler()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitNotifications();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(NotificationDeliveryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitNotifications_WithConfigure_InvokesCallback()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        bool invoked = false;
        builder.AddGranitNotifications(opts =>
        {
            opts.MaxParallelDeliveries = 16;
            invoked = true;
        });

        invoked.ShouldBeTrue();
    }

    [Fact]
    public void AddGranitNotifications_DefaultMaxParallelDeliveries_IsEight()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitNotifications();
        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        IOptions<NotificationsOptions> options = sp.GetRequiredService<IOptions<NotificationsOptions>>();

        options.Value.MaxParallelDeliveries.ShouldBe(8);
    }

    [Fact]
    public void AddNotificationDefinitions_RegistersProvider()
    {
        ServiceCollection services = new();
        services.AddNotificationDefinitions<TestDefinitionProvider>();

        using ServiceProvider sp = services.BuildServiceProvider();
        INotificationDefinitionProvider provider = sp.GetRequiredService<INotificationDefinitionProvider>();

        provider.ShouldBeOfType<TestDefinitionProvider>();
    }

    [Fact]
    public void AddNotificationDefinitions_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddNotificationDefinitions<TestDefinitionProvider>();

        result.ShouldBeSameAs(services);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private sealed class TestDefinitionProvider : INotificationDefinitionProvider
    {
        public void Define(INotificationDefinitionContext context) =>
            context.Add(new NotificationDefinition("test.notification")
            {
                DefaultChannels = [NotificationChannels.InApp],
            });
    }
}
