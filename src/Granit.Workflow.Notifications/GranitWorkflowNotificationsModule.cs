using Granit.Authorization;
using Granit.Identity;
using Granit.Modularity;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Granit.Templating;
using Granit.Templating.Extensions;
using Granit.Workflow.Notifications.Extensions;
using Granit.Workflow.Notifications.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Workflow.Notifications;

/// <summary>
/// Granit module for the workflow approval notification bridge.
/// Routes <see cref="Events.WorkflowApprovalRequestedEvent"/> events to designated approvers
/// via <c>Granit.Notifications</c> and ships the embedded HTML templates for the email
/// channel of <c>workflow.approval_requested</c>.
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
    typeof(GranitIdentityAbstractionsModule),
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitTemplatingModule),
    typeof(GranitWorkflowModule))]
public sealed class GranitWorkflowNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitWorkflowNotifications();

        // Ship the embedded HTML templates for the workflow email notifications.
        // Apps can override any of them at runtime through the Granit.Templating
        // admin API (DB-backed resolver runs at higher priority than the embedded one).
        context.Services.AddEmbeddedTemplates(typeof(GranitWorkflowNotificationsModule).Assembly);

        // Layout glob — every workflow notification email uses the host's `Layout.Email`
        // wrapper if registered. If absent, templates render without layout (warning logged,
        // no crash).
        context.Services.AddTemplateLayout("workflow.*", "Layout.Email");

        context.Services.AddSingleton<INotificationDefinitionProvider, WorkflowNotificationDefinitionProvider>();
    }
}
