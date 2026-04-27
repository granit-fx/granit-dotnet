using Granit.Notifications;

namespace Granit.Identity.Federated.Notifications;

/// <summary>
/// Notification raised when a user-sync between the upstream identity provider
/// and the local <c>UserCacheEntry</c> table fails. Provides the human-readable
/// signal ISO 27001 A.12.4 expects on top of the existing log line — drift
/// between IdP and our user cache must reach a responder, not only the SIEM.
/// </summary>
/// <remarks>
/// <para>
/// Channels: Email + InApp. <see cref="NotificationSeverity.Warning"/> — a sync
/// failure is not, by itself, a security incident, but accumulating failures
/// silently is what A.12.4 explicitly forbids. Operators acknowledge or
/// escalate per the platform runbook.
/// </para>
/// <para>
/// The producer side rate-limits emissions per (UserId, ProviderName) per
/// configurable cool-off window (default 60 minutes) so a sync-loop does not
/// flood operator inboxes — see <c>IdentityFederatedNotificationOptions</c>.
/// </para>
/// </remarks>
public sealed class IdentitySyncFailedNotificationType
    : NotificationType<IdentitySyncFailedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly IdentitySyncFailedNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "identity.sync_failed";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Data payload for the federated identity sync-failed notification.
/// </summary>
/// <remarks>
/// No PII beyond the opaque user identifier — names and email addresses are
/// intentionally omitted (data minimisation, GDPR Art. 5(1)(c)). Responders
/// pivot from <paramref name="UserId"/> into the user-cache admin page if a
/// fuller profile is required.
/// </remarks>
/// <param name="UserId">External identifier of the user whose sync failed.</param>
/// <param name="ProviderName">Logical provider name (e.g. <c>"Keycloak"</c>).</param>
/// <param name="Reason">Short, human-readable failure reason (already free of PII at the producer).</param>
/// <param name="OccurredAt">UTC timestamp of the sync failure.</param>
/// <param name="TenantId">Optional tenant scope of the failed sync.</param>
public sealed record IdentitySyncFailedNotificationData(
    string UserId,
    string ProviderName,
    string Reason,
    DateTimeOffset OccurredAt,
    Guid? TenantId);
