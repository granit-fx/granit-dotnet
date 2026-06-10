using Granit.Notifications;

namespace Granit.Identity.Local.Notifications.NotificationTypes;

/// <summary>
/// Notification type for the one-time code delivered by email for the email-based
/// two-factor method (enrollment verification or login challenge).
/// </summary>
public sealed class TwoFactorEmailOtpNotificationType
    : NotificationType<TwoFactorEmailOtpNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly TwoFactorEmailOtpNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "identity.two_factor_email_otp";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email];
}

/// <summary>
/// Data payload for a two-factor email one-time-code notification.
/// </summary>
/// <param name="Email">The user's email address.</param>
/// <param name="Code">The one-time verification code.</param>
#pragma warning disable GRSEC003 // Code is a transient OTP payload, not a stored secret
public sealed record TwoFactorEmailOtpNotificationData(string Email, string Code);
#pragma warning restore GRSEC003
