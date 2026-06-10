using Granit.Notifications;

namespace Granit.Identity.Local.Notifications.NotificationTypes;

/// <summary>
/// Notification type for the email confirmation link sent during registration or resend.
/// </summary>
public sealed class EmailConfirmationNotificationType
    : NotificationType<EmailConfirmationNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly EmailConfirmationNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "identity.email_confirmation";

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email];
}

/// <summary>
/// Data payload for an email confirmation notification.
/// </summary>
/// <param name="Email">The email address to confirm.</param>
/// <param name="ConfirmLink">The email confirmation URL.</param>
public sealed record EmailConfirmationNotificationData(string Email, string ConfirmLink);
