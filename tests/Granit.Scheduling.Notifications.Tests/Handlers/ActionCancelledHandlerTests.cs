using Granit.Notifications.Abstractions;
using Granit.Scheduling.Events;
using Granit.Scheduling.Notifications.Handlers;
using NSubstitute;
using Xunit;

namespace Granit.Scheduling.Notifications.Tests.Handlers;

public sealed class ActionCancelledHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesActionCancelledNotification_ToSubscribers()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var actionId = Guid.NewGuid();
        const string correlationId = "invoice-2026-04";
        const string cancelledBy = "jf.meyers@digitaldynamics.be";
        ScheduledActionCancelledEvent evt = new(actionId, correlationId, cancelledBy);

        await ActionCancelledHandler.HandleAsync(evt, publisher, CancellationToken.None);

        await publisher.Received(1).PublishToSubscribersAsync(
            SchedulingActionCancelledNotificationType.Instance,
            Arg.Is<SchedulingActionCancelledNotificationData>(d =>
                d.ActionId == actionId
                && d.CorrelationId == correlationId
                && d.CancelledBy == cancelledBy),
            Arg.Any<CancellationToken>());
    }
}
