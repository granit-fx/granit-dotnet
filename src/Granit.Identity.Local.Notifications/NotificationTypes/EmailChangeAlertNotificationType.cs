using Granit.Notifications;

namespace Granit.Identity.Local.Notifications.NotificationTypes;

/// <summary>
/// Notification type for the security alert sent to the <b>current</b> email address
/// when a user requests an email change. Enables detection of unauthorized changes.
/// </summary>
public sealed class EmailChangeAlertNotificationType
    : NotificationType<EmailChangeAlertNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly EmailChangeAlertNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "Security.EmailChangeAlert";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email];
}

/// <summary>
/// Data payload for an email change alert notification.
/// </summary>
/// <param name="Email">The user's current email address.</param>
/// <param name="NewEmail">The requested new email address.</param>
public sealed record EmailChangeAlertNotificationData(string Email, string NewEmail);
