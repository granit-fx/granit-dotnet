using Granit.Notifications;

namespace Granit.Identity.Federated.Notifications;

/// <summary>
/// Notification type raised when a federated identity provider has confirmed a
/// user deletion and the local user-cache entry has been hard-deleted in
/// response. Acts as a GDPR Art. 17 erasure receipt for tenant administrators —
/// a human-readable confirmation that the right-to-erasure obligation has been
/// honoured by the application layer, not just the upstream IdP.
/// </summary>
/// <remarks>
/// <para>
/// Channels: Email + InApp. <see cref="NotificationSeverity.Info"/> — the
/// deletion is the expected outcome of an authorised flow (IdP webhook,
/// scheduled provisioning job, manual admin action), not an incident. The
/// notification exists to make the receipt auditable and to give the tenant
/// admin a heads-up that the local cache is now empty for the user, which can
/// affect downstream features (invoice history, audit log attribution).
/// </para>
/// <para>
/// Recipients are resolved via the standard notifications subscription system —
/// tenant administrators opt in through the admin UI.
/// </para>
/// </remarks>
public sealed class IdentityUserProvisioningRemovedNotificationType
    : NotificationType<IdentityUserProvisioningRemovedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly IdentityUserProvisioningRemovedNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "identity.user_provisioning_removed";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Info;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Data payload for the federated user-provisioning-removed (GDPR Art. 17)
/// receipt notification.
/// </summary>
/// <remarks>
/// The payload carries the opaque external user identifier and the optional
/// tenant scope only — the user's name and email are intentionally omitted
/// (data minimisation; the deletion has already happened, broadcasting the
/// erased identity in plain text in a notification email would defeat the
/// purpose).
/// </remarks>
/// <param name="UserId">External identifier of the user whose cache entry was hard-deleted.</param>
/// <param name="TenantId">Tenant scope of the deletion. <see langword="null"/> means the
/// deletion was applied across all tenants (cross-tenant identity).</param>
/// <param name="OccurredAt">UTC timestamp at which the cache entry was erased.</param>
public sealed record IdentityUserProvisioningRemovedNotificationData(
    string UserId,
    Guid? TenantId,
    DateTimeOffset OccurredAt);
