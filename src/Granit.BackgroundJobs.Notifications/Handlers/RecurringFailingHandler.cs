using Granit.BackgroundJobs.Events;
using Granit.Notifications.Abstractions;

namespace Granit.BackgroundJobs.Notifications.Handlers;

/// <summary>
/// Handles <see cref="BackgroundJobFailureThresholdExceededEto"/> by publishing a
/// <see cref="JobsRecurringFailingNotificationType"/> to every administrator subscribed
/// to the notification type.
/// </summary>
/// <remarks>
/// <para>
/// Recipients are resolved via <see cref="INotificationPublisher.PublishToSubscribersAsync"/>
/// — administrators opt in through the notifications admin UI. This avoids hardcoding
/// email addresses in options and naturally supports per-tenant routing: the dispatch
/// engine intersects subscribers with the active tenant context.
/// </para>
/// <para>
/// Wolverine routing: the originating Eto is fired by <c>BackgroundJobDefinition</c>,
/// which lives in the same process as the <c>*.Notifications</c> bridge in every Granit
/// host shipping <c>Granit.BackgroundJobs</c>. Wolverine therefore dispatches the message
/// in-process — no outbox / queue round trip. If a future deployment splits the worker
/// from the API, the Eto (which implements <c>Granit.Events.IIntegrationEvent</c>)
/// will traverse the bus without code changes.
/// </para>
/// </remarks>
public class RecurringFailingHandler
{
    public static async Task HandleAsync(
        BackgroundJobFailureThresholdExceededEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.PublishToSubscribersAsync(
            JobsRecurringFailingNotificationType.Instance,
            new JobsRecurringFailingNotificationData(
                evt.JobId,
                evt.JobName,
                evt.ConsecutiveFailureCount,
                evt.LastErrorMessage),
            cancellationToken).ConfigureAwait(false);
    }
}
