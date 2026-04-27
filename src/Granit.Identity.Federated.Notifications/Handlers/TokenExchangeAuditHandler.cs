using Granit.Identity.Federated.Events;
using Granit.Notifications.Abstractions;

namespace Granit.Identity.Federated.Notifications.Handlers;

/// <summary>
/// Handles <see cref="IdentityTokenExchangedEto"/> by publishing an
/// <see cref="IdentityTokenExchangeAuditNotificationType"/> to every platform
/// administrator subscribed to the notification type. Provides a real-time
/// human-readable companion to the SIEM trail required by ISO 27001 A.12.4
/// for privileged operations (impersonation / on-behalf-of).
/// </summary>
/// <remarks>
/// <para>
/// <b>Recipient strategy.</b> The Eto carries no addressee — it is the audit
/// trail itself. Rather than coupling this bridge to any platform-admin
/// roster service, it publishes via
/// <see cref="INotificationPublisher.PublishToSubscribersAsync{TData}"/>:
/// platform administrators / on-call operators opt in through the standard
/// notifications subscription UI, and the dispatch engine fans the alert out
/// to whoever is subscribed at delivery time.
/// </para>
/// <para>
/// <b>Wolverine routing — distributed.</b> <see cref="IdentityTokenExchangedEto"/>
/// is an integration event that can traverse process / service boundaries
/// (the federated identity flow is often split between a public-facing API
/// and a back-office or SIEM-adjacent worker). Wolverine routes it through
/// the configured transport (queue / topic) — the handler does not assume
/// in-process delivery.
/// </para>
/// </remarks>
public class TokenExchangeAuditHandler
{
    public static async Task HandleAsync(
        IdentityTokenExchangedEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);
        ArgumentNullException.ThrowIfNull(publisher);

        await publisher.PublishToSubscribersAsync(
            IdentityTokenExchangeAuditNotificationType.Instance,
            new IdentityTokenExchangeAuditNotificationData(
                evt.TargetUserId,
                evt.Reason,
                evt.OccurredAt),
            cancellationToken).ConfigureAwait(false);
    }
}
