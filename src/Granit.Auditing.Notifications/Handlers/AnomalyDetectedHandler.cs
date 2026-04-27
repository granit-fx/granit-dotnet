using Granit.Auditing.Events;
using Granit.Auditing.Notifications.Options;
using Granit.Notifications.Abstractions;
using Microsoft.Extensions.Options;

namespace Granit.Auditing.Notifications.Handlers;

/// <summary>
/// Handles <see cref="AuditEntryPersistedEto"/> by republishing the subset of
/// entries whose <see cref="Domain.AuditCategory"/> is in the configured
/// alertable set as an
/// <see cref="AuditingAnomalyDetectedNotificationType"/>. Non-alertable entries
/// (regular CRUD, opt-in data-access reads) are dropped without a publish call,
/// keeping the SOC inbox focused on actual security signals.
/// </summary>
/// <remarks>
/// <para>
/// <b>Recipient strategy.</b> The persistence Eto carries no addressee — it is the
/// audit log itself. Rather than coupling this bridge to any tenant-admin or
/// SOC-roster resolver, it publishes via
/// <see cref="INotificationPublisher.PublishToSubscribersAsync{TData}"/>: platform
/// administrators and on-call operators opt in through the standard notifications
/// subscription UI, and the dispatch engine fans the alert out to whoever is
/// subscribed at delivery time.
/// </para>
/// <para>
/// <b>Wolverine routing — local.</b> <see cref="AuditEntryPersistedEto"/> is
/// emitted in-process by <c>Granit.Auditing</c>'s persistence pipeline. Both the
/// audit batch persister and this handler load in the same host that ships
/// <c>Granit.Auditing.Notifications</c>, so Wolverine subscribes via the local
/// event bus — no broker hop, no outbox round-trip — minimising latency between
/// the suspect event and the responder's pager.
/// </para>
/// </remarks>
public class AnomalyDetectedHandler
{
    public static async Task HandleAsync(
        AuditEntryPersistedEto evt,
        INotificationPublisher publisher,
        IOptions<AuditNotificationOptions> options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(options);

        AuditNotificationOptions configured = options.Value;
        if (!configured.AlertableCategories.Contains(evt.Category))
        {
            return;
        }

        await publisher.PublishToSubscribersAsync(
            AuditingAnomalyDetectedNotificationType.Instance,
            new AuditingAnomalyDetectedNotificationData(
                evt.Id,
                evt.Category.ToString(),
                evt.Timestamp,
                evt.UserId,
                evt.EntityChangeCount,
                evt.TenantId),
            cancellationToken).ConfigureAwait(false);
    }
}
