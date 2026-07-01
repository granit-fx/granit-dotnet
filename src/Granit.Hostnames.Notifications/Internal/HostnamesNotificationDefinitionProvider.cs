using Granit.Notifications;
using Granit.Notifications.Abstractions;

namespace Granit.Hostnames.Notifications.Internal;

/// <summary>
/// Registers hostname notification definitions (group, channels, opt-out posture).
/// </summary>
internal sealed class HostnamesNotificationDefinitionProvider : INotificationDefinitionProvider
{
    private const string GroupName = "Hostnames";

    public void Define(INotificationDefinitionContext context)
    {
        context.Add(new NotificationDefinition(HostnameVerifiedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Hostname Verified",
            Description = "Sent to the resource owner when DNS verification succeeds and the hostname becomes active.",
            DefaultSeverity = NotificationSeverity.Success,
            DefaultChannels = [NotificationChannels.Email],
            AllowUserOptOut = true,
        });

        context.Add(new NotificationDefinition(HostnameVerificationFailedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Hostname Verification Failed",
            Description = "Sent to the resource owner when DNS verification fails. Includes the detected conflicts and the next automatic retry time.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.Email],
            AllowUserOptOut = true,
        });

        context.Add(new NotificationDefinition(HostnamesCertificateSecuredNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Certificate Secured",
            Description = "Sent to the resource owner when the edge provider issues an SSL/TLS certificate for their hostname.",
            DefaultSeverity = NotificationSeverity.Success,
            DefaultChannels = [NotificationChannels.Email],
            AllowUserOptOut = true,
        });

        context.Add(new NotificationDefinition(HostnamesCertificateFailedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Certificate Failed",
            Description = "Sent to the resource owner when SSL/TLS certificate provisioning fails for their hostname.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.Email],
            AllowUserOptOut = true,
        });
    }
}
