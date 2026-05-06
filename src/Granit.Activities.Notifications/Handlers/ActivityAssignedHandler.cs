using Granit.Activities.Events;
using Granit.Events;
using Granit.Notifications.Abstractions;

namespace Granit.Activities.Notifications.Handlers;

/// <summary>
/// Handles <see cref="ActivityAssignedEvent"/> by routing an "assigned"
/// notification to the assignee. Local-bus dispatch — fires within the same
/// scope as the originating Create / Reassign.
/// </summary>
public class ActivityAssignedHandler(INotificationPublisher publisher)
    : ILocalEventHandler<ActivityAssignedEvent>
{
    public async Task HandleAsync(ActivityAssignedEvent evt, CancellationToken cancellationToken = default) =>
        await publisher.PublishAsync(
            ActivityAssignedNotificationType.Instance,
            new ActivityAssignedNotificationData(
                evt.ActivityId, evt.Type, evt.DueAt, evt.EntityType, evt.EntityId),
            recipientUserIds: [evt.AssignedToUserId.ToString()],
            cancellationToken).ConfigureAwait(false);
}
