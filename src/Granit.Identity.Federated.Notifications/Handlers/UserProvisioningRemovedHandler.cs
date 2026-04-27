using Granit.Identity.Federated.Events;
using Granit.Notifications.Abstractions;

namespace Granit.Identity.Federated.Notifications.Handlers;

/// <summary>
/// Handles <see cref="IdentityUserDeletedEto"/> by publishing an
/// <see cref="IdentityUserProvisioningRemovedNotificationType"/> as a
/// human-readable GDPR Art. 17 erasure receipt for tenant administrators.
/// </summary>
/// <remarks>
/// <para>
/// <b>Recipient strategy.</b> The Eto carries no addressee — it is the
/// IdP-driven trigger that erases the corresponding user cache entry.
/// Rather than coupling this bridge to a tenant-admin roster service, it
/// publishes via
/// <see cref="INotificationPublisher.PublishToSubscribersAsync{TData}"/>:
/// tenant administrators opt in through the standard notifications
/// subscription UI, and the dispatch engine intersects subscribers with
/// the active tenant context (<paramref name="evt"/>'s
/// <see cref="IdentityUserDeletedEto.TenantId"/> is honoured by the
/// dispatch pipeline's tenant filter).
/// </para>
/// <para>
/// <b>Wolverine routing — distributed.</b> <see cref="IdentityUserDeletedEto"/>
/// is an integration event translated from provider-specific webhooks
/// (Keycloak admin events, Entra ID change notifications, Cognito post-delete
/// triggers). It is published by an edge/worker process and consumed wherever
/// <c>Granit.Identity.Federated.Notifications</c> is loaded — Wolverine routes
/// it through the configured transport without code changes.
/// </para>
/// <para>
/// The handler runs in parallel with the core
/// <c>IdentityUserEventHandler.HandleAsync(IdentityUserDeletedEto)</c> that
/// performs the actual cache erase. Wolverine's multi-subscriber dispatch
/// guarantees the notification fires regardless of the erase outcome — the
/// receipt confirms intent (the IdP says the user is gone) which is the
/// signal the tenant admin needs to react to.
/// </para>
/// </remarks>
public class UserProvisioningRemovedHandler
{
    public static async Task HandleAsync(
        IdentityUserDeletedEto evt,
        INotificationPublisher publisher,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(timeProvider);

        await publisher.PublishToSubscribersAsync(
            IdentityUserProvisioningRemovedNotificationType.Instance,
            new IdentityUserProvisioningRemovedNotificationData(
                evt.UserId,
                evt.TenantId,
                timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
    }
}
