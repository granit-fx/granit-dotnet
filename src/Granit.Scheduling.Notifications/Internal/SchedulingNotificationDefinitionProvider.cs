using Granit.Notifications;
using Granit.Notifications.Abstractions;

namespace Granit.Scheduling.Notifications.Internal;

/// <summary>
/// Registers scheduling notification definitions. Operational alerts —
/// non-opt-outable for tenant administrators.
/// </summary>
internal sealed class SchedulingNotificationDefinitionProvider : INotificationDefinitionProvider
{
    private const string GroupName = "Operations";

    public void Define(INotificationDefinitionContext context)
    {
        context.Add(new NotificationDefinition(SchedulingActionFailedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Scheduled Action Failed",
            Description = "Alerts tenant administrators when a scheduled action (report, batch export, …) failed and needs investigation.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = false,
            RequiredPermission = "Scheduling.Actions.Manage",
        });
    }
}
