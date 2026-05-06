using Granit.Activities.Events;
using Granit.Events;
using Granit.Notifications.Abstractions;

namespace Granit.Activities.Notifications.Handlers;

/// <summary>
/// Handles <see cref="ActivityOverdueEvent"/> by sending an "overdue"
/// notification to the assignee. The event is raised by the activities
/// overdue-scan background job (story A8); idempotency on the row's
/// <c>OverdueNotifiedAt</c> column guards against re-fires.
/// </summary>
public class ActivityOverdueHandler(INotificationPublisher publisher)
    : ILocalEventHandler<ActivityOverdueEvent>
{
    public async Task HandleAsync(ActivityOverdueEvent evt, CancellationToken cancellationToken = default) =>
        await publisher.PublishAsync(
            ActivityOverdueNotificationType.Instance,
            new ActivityOverdueNotificationData(
                evt.ActivityId, evt.Type, evt.DueAt, evt.OverdueByDays, evt.EntityType, evt.EntityId),
            recipientUserIds: [evt.AssignedToUserId.ToString()],
            cancellationToken).ConfigureAwait(false);
}
