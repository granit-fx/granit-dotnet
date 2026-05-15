using Granit.Notifications.Endpoints.Permissions;
using Granit.Workspaces;
using Granit.Workspaces.Framework;

namespace Granit.Notifications.Endpoints.Workspaces;

/// <summary>
/// Grafts notifications admin entries onto the
/// <c>Granit.Framework.Communication</c> shell (per ADR-040 §IoC).
/// </summary>
internal sealed class NotificationsWorkspaceContribution : IWorkspaceContributor
{
    public void Contribute(IWorkspaceContributionContext context) =>
        context.ForWorkspace(FrameworkWorkspaceNames.Communication)
            .Section("notifications", s => s
                .DisplayKey("NotificationsEndpoints:Workspace.Section")
                .Order(0)
                .Link("/notifications", i => i
                    .DisplayKey("NotificationsEndpoints:Workspace.Item")
                    .Icon("bell")
                    .Order(0)
                    .RequiresPermission(NotificationPermissions.UserNotifications.Read)));
}
