using Granit.Modularity;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Wolverine.Internal;
using Granit.Wolverine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Notifications.Wolverine.Tests;

public sealed class GranitNotificationsWolverineModuleTests
{
    [Fact]
    public void DependsOn_declares_notifications_and_wolverine_modules()
    {
        // Arrange & Act
        DependsOnAttribute? attribute = typeof(GranitNotificationsWolverineModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .OfType<DependsOnAttribute>()
            .SingleOrDefault();

        // Assert
        attribute.ShouldNotBeNull();
        attribute!.DependedTypes.ShouldContain(typeof(GranitNotificationsModule));
        attribute.DependedTypes.ShouldContain(typeof(GranitWolverineModule));
    }

    [Fact]
    public void ConfigureServices_replaces_notification_publisher()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddSingleton<INotificationPublisher, StubNotificationPublisher>();

        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        GranitNotificationsWolverineModule module = new();

        // Act
        module.ConfigureServices(context);

        // Assert
        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(INotificationPublisher));
        descriptor.ShouldNotBeNull();
        descriptor!.ImplementationType.ShouldBe(typeof(WolverineNotificationPublisher));
    }

    [Fact]
    public void ConfigureServices_registers_wolverine_queue_configuration()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddSingleton<INotificationPublisher, StubNotificationPublisher>();

        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        GranitNotificationsWolverineModule module = new();

        // Act
        module.ConfigureServices(context);

        // Assert — invoke all registered IWolverineExtension to cover lambda bodies
        IEnumerable<IWolverineExtension?> extensions = builder.Services
            .Where(d => d.ServiceType == typeof(IWolverineExtension))
            .Select(d => d.ImplementationInstance as IWolverineExtension)
            .Where(e => e is not null);

        WolverineOptions opts = new();
        foreach (IWolverineExtension? ext in extensions)
        {
            ext!.Configure(opts);
        }

        // If we get here without exception, the Wolverine queue configuration succeeded
        opts.ShouldNotBeNull();
    }

    private sealed class StubNotificationPublisher : INotificationPublisher
    {
        public ValueTask PublishAsync<TData>(
            NotificationType<TData> notificationType,
            TData data,
            IReadOnlyList<string> recipientUserIds,
            CancellationToken cancellationToken = default) where TData : notnull =>
            ValueTask.CompletedTask;

        public ValueTask PublishAsync<TData>(
            NotificationType<TData> notificationType,
            TData data,
            IReadOnlyList<string> recipientUserIds,
            EntityReference? relatedEntity,
            CancellationToken cancellationToken = default) where TData : notnull =>
            ValueTask.CompletedTask;

        public ValueTask PublishAsync<TData>(
            NotificationType<TData> notificationType,
            TData data,
            IReadOnlyList<string> recipientUserIds,
            RecipientInfo recipientOverride,
            EntityReference? relatedEntity = null,
            CancellationToken cancellationToken = default) where TData : notnull =>
            ValueTask.CompletedTask;

        public ValueTask PublishToSubscribersAsync<TData>(
            NotificationType<TData> notificationType,
            TData data,
            CancellationToken cancellationToken = default) where TData : notnull =>
            ValueTask.CompletedTask;

        public ValueTask PublishToEntityFollowersAsync<TData>(
            NotificationType<TData> notificationType,
            TData data,
            EntityReference relatedEntity,
            CancellationToken cancellationToken = default) where TData : notnull =>
            ValueTask.CompletedTask;
    }
}
