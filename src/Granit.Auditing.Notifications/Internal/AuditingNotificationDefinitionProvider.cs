using Granit.Notifications;
using Granit.Notifications.Abstractions;

namespace Granit.Auditing.Notifications.Internal;

/// <summary>
/// Registers auditing notification definitions. Security-critical alerts —
/// non-opt-outable, feeds SIEM/oncall workflows (ISO 27001 A.12.4 / A.16.1.4).
/// </summary>
internal sealed class AuditingNotificationDefinitionProvider : INotificationDefinitionProvider
{
    private const string GroupName = "Security";

    public void Define(INotificationDefinitionContext context)
    {
        context.Add(new NotificationDefinition(AuditingAnomalyDetectedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Audit Anomaly Detected",
            Description = "Alerts platform administrators and SOC responders when the audit subsystem detects an anomalous access pattern.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = false,
            RequiredPermission = "Auditing.AuditEntries.Read",
        });
    }
}
