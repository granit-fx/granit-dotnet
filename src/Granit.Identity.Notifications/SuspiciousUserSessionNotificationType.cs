using Granit.Notifications;

namespace Granit.Identity.Notifications;

/// <summary>
/// High-tier security alert: a sign-in was assessed as <c>High</c> risk (e.g. impossible travel).
/// Sent to the account owner so they can self-verify ("yes, that's me") or secure their account.
/// </summary>
/// <remarks>
/// Hard-locked against opt-out at the definition level (a genuine suspicious-sign-in alert is a
/// security control, not a marketing email). Channel: Email only — it must reach the user as a
/// transactional security record.
/// </remarks>
public sealed class SuspiciousUserSessionNotificationType
    : NotificationType<SuspiciousUserSessionNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly SuspiciousUserSessionNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "user_sessions.suspicious_session";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email];
}
