using Granit.Notifications;

namespace Granit.Identity.Local.Notifications.NotificationTypes;

/// <summary>
/// Notification type for alerting a user that their account has been locked
/// after exceeding the maximum number of failed login attempts.
/// </summary>
public sealed class AccountLockedNotificationType
    : NotificationType<AccountLockedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly AccountLockedNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "identity.account_locked";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Error;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Data payload for an account locked notification.
/// </summary>
/// <param name="Email">The user's email address.</param>
/// <param name="FailedAttempts">Number of consecutive failed login attempts that triggered the lockout.</param>
/// <param name="ResetLink">The password reset URL for self-service account unlock.</param>
/// <param name="LockoutExpiresAt">UTC timestamp when the lockout expires (for display in user's timezone).</param>
public sealed record AccountLockedNotificationData(
    string Email, int FailedAttempts, string ResetLink, DateTimeOffset LockoutExpiresAt);
