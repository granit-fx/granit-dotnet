using Granit.Notifications;

namespace Granit.Identity.Local.Notifications.NotificationTypes;

/// <summary>
/// Notification type for the security alert sent when a user's password is changed.
/// Enables users to detect unauthorized password changes (compromise detection).
/// </summary>
public sealed class PasswordChangedNotificationType
    : NotificationType<PasswordChangedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly PasswordChangedNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "identity.password_changed";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email];
}

/// <summary>
/// Data payload for a password changed notification.
/// </summary>
/// <param name="Email">The user's email address.</param>
public sealed record PasswordChangedNotificationData(string Email);
