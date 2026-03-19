using Granit.Authorization;
using Granit.Core.Modularity;
using Granit.Identity;
using Granit.Notifications;
using Granit.Workflow.Notifications.Extensions;

namespace Granit.Workflow.Notifications;

/// <summary>
/// Granit module for the workflow approval notification bridge.
/// Routes <see cref="Events.WorkflowApprovalRequestedEvent"/> events to designated approvers
/// via <c>Granit.Notifications</c>.
/// </summary>
/// <remarks>
/// <para>
/// Register via:
/// <code>
/// services.AddGranitWorkflowNotifications();
/// </code>
/// </para>
/// <para>
/// The host application should register a real <see cref="IApproverResolver"/> implementation
/// via <c>services.AddWorkflowApproverResolver&lt;MyResolver&gt;()</c>.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitIdentityModule),
    typeof(GranitNotificationsModule),
    typeof(GranitWorkflowModule))]
public sealed class GranitWorkflowNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitWorkflowNotifications();
}
