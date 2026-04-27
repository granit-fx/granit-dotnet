using Granit.Metering.Events;
using Granit.Notifications.Abstractions;

namespace Granit.Metering.Notifications.Handlers;

/// <summary>
/// Handles <see cref="QuotaThresholdReachedEto"/> by fanning out a
/// <see cref="MeteringQuotaThresholdReachedNotificationType"/> to every administrator
/// subscribed to that notification type for the tenant.
/// </summary>
/// <remarks>
/// Recipient strategy: <see cref="INotificationPublisher.PublishToSubscribersAsync{TData}"/>.
/// Admins opt in through the notifications admin UI and are naturally tenant-scoped by
/// the subscription engine — no hardcoded email lists, no host-side configuration.
/// </remarks>
public class QuotaThresholdReachedNotificationHandler
{
    public static async Task HandleAsync(
        QuotaThresholdReachedEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.PublishToSubscribersAsync(
            MeteringQuotaThresholdReachedNotificationType.Instance,
            new MeteringQuotaThresholdReachedNotificationData(
                evt.TenantId,
                evt.MeterDefinitionId,
                evt.MeterName,
                evt.CurrentUsage,
                evt.Limit,
                evt.PercentUsed),
            cancellationToken).ConfigureAwait(false);
    }
}
