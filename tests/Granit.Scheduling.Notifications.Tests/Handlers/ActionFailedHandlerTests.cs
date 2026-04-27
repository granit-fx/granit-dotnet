using Granit.Notifications.Abstractions;
using Granit.Scheduling.Events;
using Granit.Scheduling.Notifications.Handlers;
using NSubstitute;
using Xunit;

namespace Granit.Scheduling.Notifications.Tests.Handlers;

public sealed class ActionFailedHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesActionFailedNotification_ToSubscribers()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var actionId = Guid.NewGuid();
        const string payloadType = "MonthlyComplianceReportPayload";
        const string correlationId = "invoice-2026-04";
        const string failureReason = "Connection refused: report storage endpoint unreachable.";
        DateTimeOffset failedAt = new(2026, 4, 27, 14, 30, 0, TimeSpan.Zero);
        ScheduledActionFailedEto evt = new(actionId, payloadType, correlationId, failureReason, failedAt);

        await ActionFailedHandler.HandleAsync(evt, publisher, CancellationToken.None);

        await publisher.Received(1).PublishToSubscribersAsync(
            SchedulingActionFailedNotificationType.Instance,
            Arg.Is<SchedulingActionFailedNotificationData>(d =>
                d.ActionId == actionId
                && d.PayloadType == payloadType
                && d.CorrelationId == correlationId
                && d.FailureReason == failureReason
                && d.FailedAt == failedAt),
            Arg.Any<CancellationToken>());
    }
}
