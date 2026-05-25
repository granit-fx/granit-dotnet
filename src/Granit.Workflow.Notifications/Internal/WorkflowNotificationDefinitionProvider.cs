using Granit.Notifications;
using Granit.Notifications.Abstractions;

namespace Granit.Workflow.Notifications.Internal;

/// <summary>
/// Registers workflow notification definitions. These are user-facing
/// convenience notifications — opt-out is allowed.
/// </summary>
internal sealed class WorkflowNotificationDefinitionProvider : INotificationDefinitionProvider
{
    private const string GroupName = "Workflow";

    public void Define(INotificationDefinitionContext context)
    {
        context.Add(new NotificationDefinition(WorkflowApprovalNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Workflow Approval Requested",
            Description = "Notifies an approver that a workflow step is awaiting their decision.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.InApp, NotificationChannels.Email],
            AllowUserOptOut = true,
        });

        context.Add(new NotificationDefinition(WorkflowStateChangedNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Workflow State Changed",
            Description = "Notifies workflow participants when an instance transitions to a new state.",
            DefaultSeverity = NotificationSeverity.Info,
            DefaultChannels = [NotificationChannels.InApp, NotificationChannels.SignalR],
            AllowUserOptOut = true,
        });
    }
}
