using Granit.Activities.Events;
using Granit.Events;
using Granit.Notifications.Abstractions;

namespace Granit.Activities.Notifications.Handlers;

/// <summary>
/// Handles <see cref="ActivityAssignedEvent"/> by routing an "assigned"
/// notification to the assignee. Local-bus dispatch — fires within the same
/// scope as the originating Create / Reassign.
/// </summary>
public class ActivityAssignedHandler : ILocalEventHandler<ActivityAssignedEvent>
{
    private readonly INotificationPublisher _publisher;

    public ActivityAssignedHandler(INotificationPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task HandleAsync(ActivityAssignedEvent evt, CancellationToken cancellationToken = default) =>
        await _publisher.PublishAsync(
            ActivityAssignedNotificationType.Instance,
            new ActivityAssignedNotificationData(
                evt.ActivityId, evt.Type, evt.DueAt, evt.EntityType, evt.EntityId),
            recipientUserIds: [evt.AssignedToUserId.ToString()],
            cancellationToken).ConfigureAwait(false);
}
