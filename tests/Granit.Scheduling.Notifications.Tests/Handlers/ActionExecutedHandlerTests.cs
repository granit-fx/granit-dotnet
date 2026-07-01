using Granit.Notifications.Abstractions;
using Granit.Scheduling.Events;
using Granit.Scheduling.Notifications.Handlers;
using NSubstitute;
using Xunit;

namespace Granit.Scheduling.Notifications.Tests.Handlers;

public sealed class ActionExecutedHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesActionExecutedNotification_ToSubscribers()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var actionId = Guid.NewGuid();
        const string payloadType = "MonthlyComplianceReportPayload";
        const string correlationId = "invoice-2026-04";
        DateTimeOffset executedAt = new(2026, 4, 27, 14, 30, 0, TimeSpan.Zero);
        ScheduledActionExecutedEto evt = new(actionId, payloadType, correlationId, executedAt);

        await ActionExecutedHandler.HandleAsync(evt, publisher, CancellationToken.None);

        await publisher.Received(1).PublishToSubscribersAsync(
            SchedulingActionExecutedNotificationType.Instance,
            Arg.Is<SchedulingActionExecutedNotificationData>(d =>
                d.ActionId == actionId
                && d.PayloadType == payloadType
                && d.CorrelationId == correlationId
                && d.ExecutedAt == executedAt),
            Arg.Any<CancellationToken>());
    }
}
