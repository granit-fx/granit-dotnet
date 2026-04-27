using Granit.Notifications.Abstractions;
using Granit.Scheduling.Events;

namespace Granit.Scheduling.Notifications.Handlers;

/// <summary>
/// Handles <see cref="ScheduledActionFailedEto"/> by publishing a
/// <see cref="SchedulingActionFailedNotificationType"/> to every administrator subscribed
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
/// Wolverine routing: the originating Eto is fired by <c>ScheduledAction</c>, which lives
/// in the same process as the <c>*.Notifications</c> bridge in every Granit host shipping
/// <c>Granit.Scheduling</c>. Wolverine therefore dispatches the message in-process — no
/// outbox / queue round trip. If a future deployment splits the worker from the API, the
/// Eto (which implements <c>Granit.Events.IIntegrationEvent</c>) traverses the bus
/// without code changes.
/// </para>
/// </remarks>
public class ActionFailedHandler
{
    public static async Task HandleAsync(
        ScheduledActionFailedEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.PublishToSubscribersAsync(
            SchedulingActionFailedNotificationType.Instance,
            new SchedulingActionFailedNotificationData(
                evt.ActionId,
                evt.PayloadType,
                evt.CorrelationId,
                evt.FailureReason,
                evt.FailedAt),
            cancellationToken).ConfigureAwait(false);
    }
}
