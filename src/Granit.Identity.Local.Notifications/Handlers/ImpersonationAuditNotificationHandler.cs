using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.Auditing.Events;
using Granit.Identity.Local.Auditing;
using Granit.Identity.Local.Notifications.NotificationTypes;
using Granit.Notifications.Abstractions;

namespace Granit.Identity.Local.Notifications.Handlers;

/// <summary>
/// Sends the impersonation transparency notification, derived from the durable audit trail: when an
/// impersonation audit entry is persisted, the impersonated user is alerted (GDPR/SOC2 compliance).
/// </summary>
/// <remarks>
/// The impersonating endpoint writes the durable audit entry — the compliance source of truth — and
/// this handler reacts to the resulting <see cref="AuditEntryPersistedEto"/>. It filters cheaply on
/// the entry's <see cref="AuditEntryPersistedEto.PrimaryEntityType"/> (only impersonation entries
/// carry <see cref="ImpersonationAuditMarker.AuditEntityType"/>), then loads the persisted entry to
/// read the impersonated user (the synthetic change's <c>EntityId</c>), the impersonator's display
/// name (the entry's actor) and the timestamp. Keeping the notification a subscriber of the audit
/// record — rather than an inline publish at the impersonation site — leaves the endpoint with a
/// single responsibility and the audit entry as the one source of truth.
/// </remarks>
public class ImpersonationAuditNotificationHandler
{
    public static async Task HandleAsync(
        AuditEntryPersistedEto evt,
        IAuditingReader auditingReader,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        if (evt.Category != AuditCategory.PrivilegedAccess
            || evt.PrimaryEntityType != ImpersonationAuditMarker.AuditEntityType)
        {
            return;
        }

        AuditEntry? entry = await auditingReader.GetByIdAsync(evt.Id, cancellationToken).ConfigureAwait(false);
        AuditEntityChange? change = entry?.EntityChanges.FirstOrDefault();
        if (change is null || !Guid.TryParse(change.EntityId, out Guid targetUserId))
        {
            return;
        }

        await publisher.PublishAsync(
            ImpersonationAlertNotificationType.Instance,
            new ImpersonationAlertNotificationData(entry!.Timestamp, entry.UserName),
            [targetUserId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
