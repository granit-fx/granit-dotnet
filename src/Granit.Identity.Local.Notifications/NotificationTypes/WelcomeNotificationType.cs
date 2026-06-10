using Granit.Notifications;

namespace Granit.Identity.Local.Notifications.NotificationTypes;

/// <summary>
/// Notification type for the welcome email sent after user registration.
/// </summary>
public sealed class WelcomeNotificationType
    : NotificationType<WelcomeNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly WelcomeNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "identity.welcome";

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email];
}

/// <summary>
/// Data payload for a welcome notification.
/// </summary>
/// <param name="Email">The registered user's email address.</param>
public sealed record WelcomeNotificationData(string Email);
