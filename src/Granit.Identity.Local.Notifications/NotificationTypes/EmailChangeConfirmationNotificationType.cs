using Granit.Notifications;

namespace Granit.Identity.Local.Notifications.NotificationTypes;

/// <summary>
/// Notification type for the confirmation link sent to the <b>new</b> email address
/// when a user requests an email change. The user must click the link to complete the change.
/// </summary>
public sealed class EmailChangeConfirmationNotificationType
    : NotificationType<EmailChangeConfirmationNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly EmailChangeConfirmationNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "Security.EmailChangeConfirmation";

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email];
}

/// <summary>
/// Data payload for an email change confirmation notification.
/// </summary>
/// <param name="NewEmail">The requested new email address.</param>
/// <param name="ConfirmLink">The email change confirmation URL.</param>
public sealed record EmailChangeConfirmationNotificationData(string NewEmail, string ConfirmLink);
