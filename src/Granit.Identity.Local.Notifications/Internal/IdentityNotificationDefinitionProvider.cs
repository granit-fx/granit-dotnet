using Granit.Identity.Local.Notifications.NotificationTypes;
using Granit.Notifications;
using Granit.Notifications.Abstractions;

namespace Granit.Identity.Local.Notifications.Internal;

/// <summary>
/// Registers all identity notification definitions with their metadata
/// (group, display name, opt-out policy).
/// </summary>
internal sealed class IdentityNotificationDefinitionProvider : INotificationDefinitionProvider
{
    private const string GroupName = "Security";

    public void Define(INotificationDefinitionContext context)
    {
        // Welcome — the only identity notification users can opt out of
        context.Add(new NotificationDefinition(WelcomeNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Welcome",
            Description = "Welcome email sent after user registration.",
            DefaultSeverity = NotificationSeverity.Info,
            DefaultChannels = [NotificationChannels.Email],
            AllowUserOptOut = true,
        });

        // Password reset — transactional, cannot be opted out
        context.Add(new NotificationDefinition(PasswordResetNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Password Reset",
            Description = "Password reset link sent during the forgot-password flow.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.Email],
            AllowUserOptOut = false,
        });

        // Email confirmation — transactional, cannot be opted out
        context.Add(new NotificationDefinition(EmailConfirmationNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Email Confirmation",
            Description = "Email confirmation link sent during registration or resend.",
            DefaultSeverity = NotificationSeverity.Info,
            DefaultChannels = [NotificationChannels.Email],
            AllowUserOptOut = false,
        });

        // Password changed — security alert, cannot be opted out
        context.Add(new NotificationDefinition(PasswordChangedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Password Changed",
            Description = "Security alert when a user's password is changed.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.Email],
            AllowUserOptOut = false,
        });

        // Account locked — security alert, cannot be opted out
        context.Add(new NotificationDefinition(AccountLockedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Account Locked",
            Description = "Alert when a user account is locked after failed login attempts.",
            DefaultSeverity = NotificationSeverity.Error,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = false,
        });

        // Two-factor changed — security alert, cannot be opted out
        context.Add(new NotificationDefinition(TwoFactorChangedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Two-Factor Authentication Changed",
            Description = "Security alert when 2FA is enabled or disabled on an account.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.Email],
            AllowUserOptOut = false,
        });

        // Email change alert — security alert to current email, cannot be opted out
        context.Add(new NotificationDefinition(EmailChangeAlertNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Email Change Alert",
            Description = "Security alert sent to the current email when a change is requested.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.Email],
            AllowUserOptOut = false,
        });

        // Email change confirmation — transactional to new email, cannot be opted out
        context.Add(new NotificationDefinition(EmailChangeConfirmationNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Email Change Confirmation",
            Description = "Confirmation link sent to the new email address to complete the change.",
            DefaultSeverity = NotificationSeverity.Info,
            DefaultChannels = [NotificationChannels.Email],
            AllowUserOptOut = false,
        });

        // Impersonation alert — GDPR/SOC2 compliance, cannot be opted out
        context.Add(new NotificationDefinition(ImpersonationAlertNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Impersonation Alert",
            Description = "Transparency notification when an administrator impersonates the user.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = false,
        });
    }
}
