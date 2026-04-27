using Granit.Identity.Federated.Events;
using Granit.Notifications.Abstractions;

namespace Granit.Identity.Federated.Notifications.Handlers;

/// <summary>
/// Handles <see cref="IdentityUserSyncFailedEto"/> by publishing an
/// <see cref="IdentitySyncFailedNotificationType"/> to every platform
/// administrator subscribed to the <c>identity.sync_failed</c> topic. Provides
/// the real-time human-readable companion to the existing log line that
/// ISO 27001 A.12.4 expects for IdP/local-cache drift detection.
/// </summary>
/// <remarks>
/// <para>
/// <b>Recipient strategy.</b> The Eto carries no addressee — the producer
/// already rate-limits emissions per (UserId, ProviderName) per cool-off
/// window so the dispatch engine does not need to deduplicate again. Platform
/// administrators / on-call operators opt in through the standard
/// notifications subscription UI and the publisher fans the alert out via
/// <see cref="INotificationPublisher.PublishToSubscribersAsync{TData}"/>; SOC
/// integrations that prefer SIEM routing can subscribe directly to the Eto
/// without going through this notification at all.
/// </para>
/// <para>
/// <b>Wolverine routing — distributed.</b> <see cref="IdentityUserSyncFailedEto"/>
/// is an integration event emitted by the federated identity layer (often the
/// API or a webhook ingestor) and consumed wherever
/// <c>Granit.Identity.Federated.Notifications</c> is loaded. Wolverine routes
/// it through the configured transport without code changes, matching the
/// pattern used by <see cref="TokenExchangeAuditHandler"/> and
/// <see cref="UserProvisioningRemovedHandler"/>.
/// </para>
/// </remarks>
public class SyncFailedHandler
{
    public static async Task HandleAsync(
        IdentityUserSyncFailedEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);
        ArgumentNullException.ThrowIfNull(publisher);

        await publisher.PublishToSubscribersAsync(
            IdentitySyncFailedNotificationType.Instance,
            new IdentitySyncFailedNotificationData(
                evt.UserId,
                evt.ProviderName,
                evt.Reason,
                evt.OccurredAt,
                evt.TenantId),
            cancellationToken).ConfigureAwait(false);
    }
}
