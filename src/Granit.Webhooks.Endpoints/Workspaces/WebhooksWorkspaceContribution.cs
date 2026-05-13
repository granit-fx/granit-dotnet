using Granit.Webhooks.Endpoints.Permissions;
using Granit.Workspaces;
using Granit.Workspaces.Framework;

namespace Granit.Webhooks.Endpoints.Workspaces;

/// <summary>
/// Grafts webhooks admin entries onto the
/// <c>Granit.Framework.Integrations</c> shell (per ADR-040 §IoC).
/// </summary>
internal sealed class WebhooksWorkspaceContribution : IWorkspaceContributor
{
    public void Contribute(IWorkspaceContributionContext context) =>
        context.ForWorkspace(FrameworkWorkspaceNames.Integrations)
            .Section("webhooks", s => s
                .DisplayKey("WebhooksEndpoints:Workspace.Section")
                .Order(0)
                .Link("/webhooks", i => i
                    .DisplayKey("WebhooksEndpoints:Workspace.Item")
                    .Icon("webhook")
                    .Order(0)
                    .RequiresPermission(WebhooksPermissions.Subscriptions.Read)));
}
