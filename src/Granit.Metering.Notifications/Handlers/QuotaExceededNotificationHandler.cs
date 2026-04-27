using Granit.Metering.Events;
using Granit.Notifications.Abstractions;

namespace Granit.Metering.Notifications.Handlers;

/// <summary>
/// Handles <see cref="QuotaExceededEto"/> by fanning out a
/// <see cref="MeteringQuotaExceededNotificationType"/> to every administrator subscribed
/// to that notification type for the tenant.
/// </summary>
/// <remarks>
/// Recipient strategy: <see cref="INotificationPublisher.PublishToSubscribersAsync{TData}"/>.
/// Admins opt in through the notifications admin UI and are naturally tenant-scoped by
/// the subscription engine.
/// <para>
/// <see cref="QuotaExceededEto"/> does not carry a <c>PercentUsed</c> field, so we
/// derive it locally as <c>CurrentUsage / Limit * 100</c> (guarding against
/// <c>Limit == 0</c>). Values above 100 indicate overage and are surfaced to the
/// template for transparency.
/// </para>
/// </remarks>
public class QuotaExceededNotificationHandler
{
    public static async Task HandleAsync(
        QuotaExceededEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        decimal percentUsed = evt.Limit > 0m
            ? evt.CurrentUsage / evt.Limit * 100m
            : 100m;

        await publisher.PublishToSubscribersAsync(
            MeteringQuotaExceededNotificationType.Instance,
            new MeteringQuotaExceededNotificationData(
                evt.TenantId,
                evt.MeterDefinitionId,
                evt.MeterName,
                evt.CurrentUsage,
                evt.Limit,
                percentUsed),
            cancellationToken).ConfigureAwait(false);
    }
}
