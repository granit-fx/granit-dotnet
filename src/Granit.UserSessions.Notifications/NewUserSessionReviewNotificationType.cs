using Granit.Notifications;

namespace Granit.UserSessions.Notifications;

/// <summary>
/// Medium-tier informational alert: a sign-in looked unusual (e.g. new country / new device) but
/// did not warrant a full security response. Sent with a reassuring "if this was you, no action
/// needed" tone so the user can review it.
/// </summary>
/// <remarks>
/// Opt-out-able at the definition level (informational, not a security control). Reuses
/// <see cref="SuspiciousUserSessionNotificationData"/> — the same payload renders both tiers,
/// the template adjusts the tone by reason. Channel: Email only.
/// </remarks>
public sealed class NewUserSessionReviewNotificationType
    : NotificationType<SuspiciousUserSessionNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly NewUserSessionReviewNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "user_sessions.new_session_review";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Info;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email];
}
