using Granit.Notifications;

namespace Granit.Identity.Federated.Notifications;

/// <summary>
/// Notification type raised every time the federated identity layer exchanges
/// service-account credentials for a user-scoped access token (RFC 8693 — direct
/// naked impersonation). Pages platform administrators / SOC so the audit trail
/// surfaces in real time, not just in a quarterly log review.
/// </summary>
/// <remarks>
/// <para>
/// Channels: Email + InApp. <see cref="NotificationSeverity.Warning"/> is the
/// sensible default — token exchange grants a service the ability to act as any
/// registered user without their consent. ISO 27001 A.12.4 expects every such
/// privileged operation to leave a human-readable trail; treating each one as a
/// signal worth a notification (rather than only a SIEM line) keeps the security
/// posture visible to operators.
/// </para>
/// <para>
/// Recipients are resolved via the standard notifications subscription system —
/// platform administrators / on-call operators opt in through the admin UI.
/// </para>
/// </remarks>
public sealed class IdentityTokenExchangeAuditNotificationType
    : NotificationType<IdentityTokenExchangeAuditNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly IdentityTokenExchangeAuditNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "identity.token_exchange_audit";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Data payload for the federated token-exchange audit notification.
/// </summary>
/// <remarks>
/// The payload carries no PII beyond the opaque target user identifier — names
/// and email addresses are intentionally omitted (data minimisation, GDPR
/// Art. 5(1)(c)). Responders pivot from the <paramref name="TargetUserId"/>
/// into the user-cache admin page if a fuller profile is required.
/// </remarks>
/// <param name="TargetUserId">External identifier of the user the new token impersonates.</param>
/// <param name="Reason">Free-text reason / operation name (e.g. <c>"device-activity"</c>).</param>
/// <param name="OccurredAt">UTC timestamp of the token exchange.</param>
public sealed record IdentityTokenExchangeAuditNotificationData(
    string TargetUserId,
    string Reason,
    DateTimeOffset OccurredAt);
