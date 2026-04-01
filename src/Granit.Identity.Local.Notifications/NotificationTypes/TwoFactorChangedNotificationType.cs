using Granit.Notifications;

namespace Granit.Identity.Local.Notifications.NotificationTypes;

/// <summary>
/// Notification type for the security alert sent when two-factor authentication
/// is enabled or disabled on a user account.
/// </summary>
public sealed class TwoFactorChangedNotificationType
    : NotificationType<TwoFactorChangedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly TwoFactorChangedNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "Security.TwoFactorChanged";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email];
}

/// <summary>
/// Data payload for a two-factor authentication changed notification.
/// </summary>
/// <param name="Email">The user's email address.</param>
/// <param name="Enabled"><see langword="true"/> if 2FA was enabled; <see langword="false"/> if disabled.</param>
public sealed record TwoFactorChangedNotificationData(string Email, bool Enabled);
