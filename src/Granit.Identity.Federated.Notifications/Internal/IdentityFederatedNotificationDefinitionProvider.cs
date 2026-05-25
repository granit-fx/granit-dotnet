using Granit.Notifications;
using Granit.Notifications.Abstractions;

namespace Granit.Identity.Federated.Notifications.Internal;

/// <summary>
/// Registers federated identity notification definitions. All entries are
/// non-opt-outable — they carry ISO 27001 A.12.4 (monitoring of privileged
/// operations) and GDPR Art. 17 audit receipts.
/// </summary>
internal sealed class IdentityFederatedNotificationDefinitionProvider : INotificationDefinitionProvider
{
    private const string GroupName = "Security";

    public void Define(INotificationDefinitionContext context)
    {
        context.Add(new NotificationDefinition(IdentitySyncFailedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Identity Sync Failed",
            Description = "Alerts platform administrators when synchronization between the federated IdP and the local user cache fails.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = false,
        });

        context.Add(new NotificationDefinition(IdentityUserProvisioningRemovedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Federated User Removed",
            Description = "Tenant-admin receipt that a federated user has been hard-deleted (GDPR Art. 17 audit trail).",
            DefaultSeverity = NotificationSeverity.Info,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = false,
        });

        context.Add(new NotificationDefinition(IdentityTokenExchangeAuditNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Token Exchange Audit",
            Description = "Alerts platform administrators on token-exchange (service-account impersonation) events.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = false,
        });
    }
}
