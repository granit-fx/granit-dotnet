using Granit.Notifications;
using Granit.Notifications.Abstractions;

namespace Granit.UserSessions.Notifications.Internal;

/// <summary>
/// Registers the user-session notification definitions with their security posture.
/// The High-tier suspicious-session alert is hard-locked against opt-out (a genuine
/// suspicious-sign-in alert is a security control); the Medium-tier review alert is
/// informational and may be opted out of.
/// </summary>
internal sealed class UserSessionsNotificationDefinitionProvider : INotificationDefinitionProvider
{
    private const string GroupName = "UserSessions";

    public void Define(INotificationDefinitionContext context)
    {
        // High tier (e.g. impossible_travel): security alert, never suppressible, and it
        // bypasses Do-Not-Disturb gates — the framework doc names "suspicious login" as the
        // canonical AllowDoNotDisturbBypass use case.
        context.Add(new NotificationDefinition(SuspiciousUserSessionNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Suspicious Sign-in Detected",
            Description = "Security alert sent to the account owner when a sign-in is assessed as high risk (e.g. impossible travel). Hard-locked: never opt-out-able.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.Email],
            AllowUserOptOut = false,
            AllowDoNotDisturbBypass = true,
        });

        // Medium tier (new_country / new_device): informational, reassuring, opt-out-able.
        context.Add(new NotificationDefinition(NewUserSessionReviewNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "New Sign-in to Review",
            Description = "Informational alert sent to the account owner when a sign-in looks unusual (e.g. new country or new device) but does not warrant a full security response.",
            DefaultSeverity = NotificationSeverity.Info,
            DefaultChannels = [NotificationChannels.Email],
            AllowUserOptOut = true,
        });
    }
}
