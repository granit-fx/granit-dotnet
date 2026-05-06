using System.Diagnostics.Metrics;
using System.Text.Json;
using Granit.Domain;
using Granit.Guids;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Diagnostics;
using Granit.Notifications.Domain;
using Granit.Notifications.Handlers;
using Granit.Notifications.Messages;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class NotificationDeliveryHandlerEdgeCaseTests : IDisposable
{
    private readonly INotificationDeliveryWriter _deliveryWriter = Substitute.For<INotificationDeliveryWriter>();
    private readonly IClock _clock;
    private readonly ServiceProvider _sp;
    private readonly NotificationsMetrics _metrics;

    public NotificationDeliveryHandlerEdgeCaseTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        IMeterFactory meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new NotificationsMetrics(meterFactory);

        _clock = Substitute.For<IClock>();
        _clock.Now.Returns(_ => DateTimeOffset.UtcNow);

        _deliveryWriter.TryAcquireDeliveryAttemptAsync(Arg.Any<NotificationDeliveryAttempt>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public async Task HandleAsync_OperationCanceledException_is_not_caught()
    {
        // Arrange — OperationCanceledException should propagate, NOT be caught by the handler
        INotificationChannel channel = Substitute.For<INotificationChannel>();
        channel.Name.Returns(NotificationChannels.InApp);
        channel.SendAsync(Arg.Any<NotificationDeliveryContext>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new OperationCanceledException());

        NotificationDeliveryHandler handler = new(
            [channel], _deliveryWriter, new SimpleGuidGenerator(), _clock, NullLogger<NotificationDeliveryHandler>.Instance, _metrics);
        DeliverNotificationCommand command = BuildCommand();

        // Act & Assert — should throw OperationCanceledException, NOT NotificationDeliveryException
        await Should.ThrowAsync<OperationCanceledException>(
            () => handler.HandleAsync(command, TestContext.Current.CancellationToken));

        await _deliveryWriter.Received(1).CompleteDeliveryAttemptAsync(
            command.DeliveryId,
            false,
            Arg.Any<long>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_records_delivery_with_correct_fields()
    {
        // Arrange
        INotificationChannel channel = Substitute.For<INotificationChannel>();
        channel.Name.Returns(NotificationChannels.InApp);

        NotificationDeliveryAttempt? claimed = null;
        _deliveryWriter.TryAcquireDeliveryAttemptAsync(Arg.Any<NotificationDeliveryAttempt>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                claimed = callInfo.Arg<NotificationDeliveryAttempt>();
                return Task.FromResult(true);
            });

        NotificationDeliveryHandler handler = new(
            [channel], _deliveryWriter, new SimpleGuidGenerator(), _clock, NullLogger<NotificationDeliveryHandler>.Instance, _metrics);
        DeliverNotificationCommand command = BuildCommand();

        // Act
        await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        // Assert
        claimed.ShouldNotBeNull();
        claimed!.DeliveryId.ShouldBe(command.DeliveryId);
        claimed.NotificationId.ShouldBe(command.NotificationId);
        claimed.NotificationTypeName.ShouldBe(command.NotificationTypeName);
        claimed.ChannelName.ShouldBe(command.ChannelName);
        claimed.RecipientUserId.ShouldBe(command.RecipientUserId);
        claimed.TenantId.ShouldBe(command.TenantId);
    }

    [Fact]
    public async Task HandleAsync_failure_records_error_message()
    {
        // Arrange
        INotificationChannel channel = Substitute.For<INotificationChannel>();
        channel.Name.Returns(NotificationChannels.InApp);
        channel.SendAsync(Arg.Any<NotificationDeliveryContext>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Network failure"));

        NotificationDeliveryHandler handler = new(
            [channel], _deliveryWriter, new SimpleGuidGenerator(), _clock, NullLogger<NotificationDeliveryHandler>.Instance, _metrics);
        DeliverNotificationCommand command = BuildCommand();

        // Act
        try
        {
            await handler.HandleAsync(command, TestContext.Current.CancellationToken);
        }
        catch
        {
            // Expected
        }

        // Assert
        await _deliveryWriter.Received(1).CompleteDeliveryAttemptAsync(
            command.DeliveryId,
            false,
            Arg.Any<long>(),
            "Network failure",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_maps_related_entity_to_context()
    {
        // Arrange
        INotificationChannel channel = Substitute.For<INotificationChannel>();
        channel.Name.Returns(NotificationChannels.InApp);

        NotificationDeliveryContext? capturedContext = null;
        channel.SendAsync(Arg.Any<NotificationDeliveryContext>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedContext = callInfo.Arg<NotificationDeliveryContext>();
                return Task.CompletedTask;
            });

        NotificationDeliveryHandler handler = new(
            [channel], _deliveryWriter, new SimpleGuidGenerator(), _clock, NullLogger<NotificationDeliveryHandler>.Instance, _metrics);
        EntityReference entity = new("Patient", "pat-1");
        DeliverNotificationCommand command = BuildCommand(relatedEntity: entity);

        // Act
        await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        // Assert
        capturedContext.ShouldNotBeNull();
        capturedContext!.RelatedEntity.ShouldBe(entity);
    }

    [Fact]
    public async Task HandleAsync_maps_culture_to_context()
    {
        // Arrange
        INotificationChannel channel = Substitute.For<INotificationChannel>();
        channel.Name.Returns(NotificationChannels.InApp);

        NotificationDeliveryContext? capturedContext = null;
        channel.SendAsync(Arg.Any<NotificationDeliveryContext>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedContext = callInfo.Arg<NotificationDeliveryContext>();
                return Task.CompletedTask;
            });

        NotificationDeliveryHandler handler = new(
            [channel], _deliveryWriter, new SimpleGuidGenerator(), _clock, NullLogger<NotificationDeliveryHandler>.Instance, _metrics);
        DeliverNotificationCommand command = BuildCommand(culture: "fr-BE");

        // Act
        await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        // Assert
        capturedContext.ShouldNotBeNull();
        capturedContext!.Culture.ShouldBe("fr-BE");
    }

    [Fact]
    public async Task HandleAsync_selects_correct_channel_by_name()
    {
        // Arrange
        INotificationChannel inApp = Substitute.For<INotificationChannel>();
        inApp.Name.Returns(NotificationChannels.InApp);

        INotificationChannel email = Substitute.For<INotificationChannel>();
        email.Name.Returns(NotificationChannels.Email);

        NotificationDeliveryHandler handler = new(
            [inApp, email], _deliveryWriter, new SimpleGuidGenerator(), _clock, NullLogger<NotificationDeliveryHandler>.Instance, _metrics);
        DeliverNotificationCommand command = BuildCommand(channelName: NotificationChannels.Email);

        // Act
        await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        // Assert
        await inApp.DidNotReceive().SendAsync(Arg.Any<NotificationDeliveryContext>(), Arg.Any<CancellationToken>());
        await email.Received(1).SendAsync(Arg.Any<NotificationDeliveryContext>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static DeliverNotificationCommand BuildCommand(
        string channelName = NotificationChannels.InApp,
        EntityReference? relatedEntity = null,
        string? culture = null) => new()
        {
            DeliveryId = Guid.NewGuid(),
            NotificationId = Guid.NewGuid(),
            NotificationTypeName = "test.notification",
            RecipientUserId = "user-1",
            ChannelName = channelName,
            Severity = NotificationSeverity.Info,
            Data = JsonSerializer.SerializeToElement(new { key = "value" }),
            RelatedEntity = relatedEntity,
            OccurredAt = DateTimeOffset.UtcNow,
            Culture = culture,
        };
}
