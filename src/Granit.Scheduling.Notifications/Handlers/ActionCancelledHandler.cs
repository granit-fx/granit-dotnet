using Granit.Notifications.Abstractions;
using Granit.Scheduling.Events;

namespace Granit.Scheduling.Notifications.Handlers;

/// <summary>
/// Handles <see cref="ScheduledActionCancelledEvent"/> by publishing a
/// <see cref="SchedulingActionCancelledNotificationType"/> to every administrator subscribed
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
/// Wolverine routing: <see cref="ScheduledActionCancelledEvent"/> implements
/// <c>Granit.Events.IDomainEvent</c> and is fired by <c>ScheduledAction</c>, which lives in
/// the same process as the <c>*.Notifications</c> bridge in every Granit host shipping
/// <c>Granit.Scheduling</c>. Wolverine therefore dispatches the message in-process via the
/// local event bus — no outbox / queue round trip.
/// </para>
/// </remarks>
public class ActionCancelledHandler
{
    public static async Task HandleAsync(
        ScheduledActionCancelledEvent evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.PublishToSubscribersAsync(
            SchedulingActionCancelledNotificationType.Instance,
            new SchedulingActionCancelledNotificationData(
                evt.ActionId,
                evt.CorrelationId,
                evt.CancelledBy),
            cancellationToken).ConfigureAwait(false);
    }
}
