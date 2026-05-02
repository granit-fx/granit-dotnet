using Granit.Activities.Events;
using Granit.Activities.Notifications.Handlers;
using Granit.Notifications.Abstractions;
using NSubstitute;
using Xunit;

namespace Granit.Activities.Notifications.Tests.Handlers;

public sealed class ActivityAssignedHandlerTests
{
    [Fact]
    public async Task HandleAsync_publishes_assigned_notification_to_assignee()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        ActivityAssignedHandler sut = new(publisher);

        var assignee = Guid.NewGuid();
        ActivityAssignedEvent evt = new(
            ActivityId: Guid.NewGuid(),
            Type: "Call",
            AssignedToUserId: assignee,
            DueAt: new DateTimeOffset(2026, 5, 10, 14, 0, 0, TimeSpan.Zero),
            EntityType: "Granit.Parties.Party",
            EntityId: Guid.NewGuid());

        await sut.HandleAsync(evt, TestContext.Current.CancellationToken);

        await publisher.Received(1).PublishAsync(
            ActivityAssignedNotificationType.Instance,
            Arg.Is<ActivityAssignedNotificationData>(d => d.ActivityId == evt.ActivityId && d.Type == "Call"),
            Arg.Is<IReadOnlyList<string>>(r => r.Count == 1 && r[0] == assignee.ToString()),
            Arg.Any<CancellationToken>());
    }
}
