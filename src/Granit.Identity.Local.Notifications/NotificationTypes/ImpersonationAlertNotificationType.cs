using Granit.Notifications;

namespace Granit.Identity.Local.Notifications;

/// <summary>
/// Notification type for alerting a user that their account was impersonated by an administrator.
/// GDPR/SOC2 compliance — cannot be opted out.
/// </summary>
public sealed class ImpersonationAlertNotificationType
    : NotificationType<ImpersonationAlertNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly ImpersonationAlertNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "Security.ImpersonationAlert";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Data payload for an impersonation alert notification.
/// </summary>
/// <param name="OccurredAt">The UTC timestamp of the impersonation.</param>
public sealed record ImpersonationAlertNotificationData(DateTimeOffset OccurredAt);
