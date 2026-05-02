using Granit.Activities.Events;
using Granit.Events;
using Granit.Notifications.Abstractions;

namespace Granit.Activities.Notifications.Handlers;

/// <summary>
/// Handles <see cref="ActivityReminderDueEvent"/> by sending a reminder
/// notification to the assignee. The event is raised by the activities
/// reminder background job (story A8) the day before due-date.
/// </summary>
public class ActivityReminderHandler : ILocalEventHandler<ActivityReminderDueEvent>
{
    private readonly INotificationPublisher _publisher;

    public ActivityReminderHandler(INotificationPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task HandleAsync(ActivityReminderDueEvent evt, CancellationToken cancellationToken = default) =>
        await _publisher.PublishAsync(
            ActivityReminderNotificationType.Instance,
            new ActivityReminderNotificationData(
                evt.ActivityId, evt.Type, evt.DueAt, evt.EntityType, evt.EntityId),
            recipientUserIds: [evt.AssignedToUserId.ToString()],
            cancellationToken).ConfigureAwait(false);
}
