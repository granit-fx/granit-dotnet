using Granit.Notifications;

namespace Granit.Identity.Local.Notifications.NotificationTypes;

/// <summary>
/// Notification type for the password reset email containing a reset link.
/// </summary>
public sealed class PasswordResetNotificationType
    : NotificationType<PasswordResetNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly PasswordResetNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "Security.PasswordReset";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email];
}

/// <summary>
/// Data payload for a password reset notification.
/// </summary>
/// <param name="Email">The user's email address.</param>
/// <param name="ResetLink">The password reset URL.</param>
public sealed record PasswordResetNotificationData(string Email, string ResetLink);
