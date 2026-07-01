using Granit.Notifications.Abstractions;
using Granit.Scheduling.Events;

namespace Granit.Scheduling.Notifications.Handlers;

/// <summary>
/// Handles <see cref="ScheduledActionExecutedEto"/> by publishing a
/// <see cref="SchedulingActionExecutedNotificationType"/> to every administrator subscribed
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
/// Wolverine routing: <see cref="ScheduledActionExecutedEto"/> implements
/// <c>Granit.Events.IIntegrationEvent</c> and is fired by <c>ScheduledAction</c> through the
/// distributed event bus (Wolverine outbox). It traverses the bus without code changes if a
/// future deployment splits the worker from the API.
/// </para>
/// </remarks>
public class ActionExecutedHandler
{
    public static async Task HandleAsync(
        ScheduledActionExecutedEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.PublishToSubscribersAsync(
            SchedulingActionExecutedNotificationType.Instance,
            new SchedulingActionExecutedNotificationData(
                evt.ActionId,
                evt.PayloadType,
                evt.CorrelationId,
                evt.ExecutedAt),
            cancellationToken).ConfigureAwait(false);
    }
}
