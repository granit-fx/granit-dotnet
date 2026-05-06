// =============================================================================
// Tests - NotificationDeliveryHandler
// =============================================================================
// Verifies channel dispatch: no-op when channel not registered, successful
// delivery audit, failure audit with rethrow, and context field correctness.
// =============================================================================

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Granit.Guids;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Diagnostics;
using Granit.Notifications.Domain;
using Granit.Notifications.Exceptions;
using Granit.Notifications.Handlers;
using Granit.Notifications.Messages;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class NotificationDeliveryHandlerTests : IDisposable
{
    private readonly INotificationChannel _channel = Substitute.For<INotificationChannel>();
    private readonly INotificationDeliveryWriter _deliveryWriter = Substitute.For<INotificationDeliveryWriter>();
    private readonly IClock _clock;
    private readonly ILogger<NotificationDeliveryHandler> _logger = NullLogger<NotificationDeliveryHandler>.Instance;
    private readonly ActivityListener _activityListener;
    private readonly ServiceProvider _sp;
    private readonly NotificationsMetrics _metrics;

    public NotificationDeliveryHandlerTests()
    {
        _deliveryWriter.TryAcquireDeliveryAttemptAsync(Arg.Any<NotificationDeliveryAttempt>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Granit.Notifications",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_activityListener);

        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        IMeterFactory meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new NotificationsMetrics(meterFactory);

        _clock = Substitute.For<IClock>();
        _clock.Now.Returns(_ => DateTimeOffset.UtcNow);
    }

    public void Dispose()
    {
        _activityListener.Dispose();
        _sp.Dispose();
    }

    [Fact]
    public async Task HandleAsync_ChannelNotRegistered_LogsWarningAndReturns()
    {
        NotificationDeliveryHandler handler = BuildHandler(channels: []);
        DeliverNotificationCommand command = BuildCommand(channelName: "Unknown");

        Func<Task> act = () => handler.HandleAsync(command, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
        await _deliveryWriter.DidNotReceive().TryAcquireDeliveryAttemptAsync(
            Arg.Any<NotificationDeliveryAttempt>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ChannelRegistered_CallsSendAsync()
    {
        _channel.Name.Returns(NotificationChannels.InApp);
        NotificationDeliveryHandler handler = BuildHandler(channels: [_channel]);
        DeliverNotificationCommand command = BuildCommand(channelName: NotificationChannels.InApp);

        await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        await _channel.Received(1).SendAsync(
            Arg.Any<NotificationDeliveryContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ChannelSucceeds_RecordsSuccessInDeliveryStore()
    {
        _channel.Name.Returns(NotificationChannels.InApp);
        NotificationDeliveryHandler handler = BuildHandler(channels: [_channel]);
        DeliverNotificationCommand command = BuildCommand(channelName: NotificationChannels.InApp);

        await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        await _deliveryWriter.Received(1).CompleteDeliveryAttemptAsync(
            command.DeliveryId,
            true,
            Arg.Any<long>(),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ChannelThrows_RecordsFailureAndRethrowsAsDeliveryException()
    {
        _channel.Name.Returns(NotificationChannels.InApp);
        _channel.SendAsync(Arg.Any<NotificationDeliveryContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Channel failed"));
        NotificationDeliveryHandler handler = BuildHandler(channels: [_channel]);
        DeliverNotificationCommand command = BuildCommand(channelName: NotificationChannels.InApp);

        Func<Task> act = () => handler.HandleAsync(command, TestContext.Current.CancellationToken);

        await Should.ThrowAsync<NotificationDeliveryException>(act);
        await _deliveryWriter.Received(1).CompleteDeliveryAttemptAsync(
            command.DeliveryId,
            false,
            Arg.Any<long>(),
            Arg.Is<string?>(m => !string.IsNullOrEmpty(m)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ContextContainsCorrectFields()
    {
        _channel.Name.Returns(NotificationChannels.InApp);
        NotificationDeliveryContext? capturedContext = null;
        _channel.SendAsync(Arg.Any<NotificationDeliveryContext>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedContext = callInfo.Arg<NotificationDeliveryContext>();
                return Task.CompletedTask;
            });

        NotificationDeliveryHandler handler = BuildHandler(channels: [_channel]);
        DeliverNotificationCommand command = BuildCommand(channelName: NotificationChannels.InApp);

        await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        capturedContext.ShouldNotBeNull();
        capturedContext!.NotificationTypeName.ShouldBe(command.NotificationTypeName);
        capturedContext.RecipientUserId.ShouldBe(command.RecipientUserId);
        capturedContext.Data.ValueKind.ShouldNotBe(JsonValueKind.Undefined);
        capturedContext.DeliveryId.ShouldBe(command.DeliveryId);
        capturedContext.Severity.ShouldBe(command.Severity);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private NotificationDeliveryHandler BuildHandler(IReadOnlyList<INotificationChannel> channels) =>
        new(channels, _deliveryWriter, new SimpleGuidGenerator(), _clock, _logger, _metrics);

    private static DeliverNotificationCommand BuildCommand(string channelName = NotificationChannels.InApp) => new()
    {
        DeliveryId = Guid.NewGuid(),
        NotificationId = Guid.NewGuid(),
        NotificationTypeName = "test.notification",
        RecipientUserId = "user-1",
        ChannelName = channelName,
        Severity = NotificationSeverity.Info,
        Data = JsonSerializer.SerializeToElement(new { key = "value" }),
        OccurredAt = DateTimeOffset.UtcNow,
    };
}
