using Granit.Modularity;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Granit.Scheduling.Notifications.Internal;
using Granit.Templating;
using Granit.Templating.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Scheduling.Notifications;

/// <summary>
/// Granit module for the scheduling notification bridge.
/// Routes <see cref="Granit.Scheduling.Events.ScheduledActionFailedEto"/> to tenant
/// administrators via <c>Granit.Notifications</c> so a failing scheduled action
/// (recurring report, batch export, periodic sync) cannot go unnoticed for days.
/// </summary>
[DependsOn(
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitSchedulingModule),
    typeof(GranitTemplatingModule))]
public sealed class GranitSchedulingNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Ship the embedded HTML templates for every scheduling notification.
        // Apps can override any of them at runtime through the Granit.Templating
        // admin API (DB-backed resolver runs at higher priority than the embedded one).
        context.Services.AddEmbeddedTemplates(typeof(GranitSchedulingNotificationsModule).Assembly);

        // Layout glob — covers all snake_case "scheduling.*" notification names. The host
        // application registers the actual `Layout.Email` template; if absent, templates
        // render without layout (warning logged, no crash).
        context.Services.AddTemplateLayout("scheduling.*", "Layout.Email");

        context.Services.AddSingleton<INotificationDefinitionProvider, SchedulingNotificationDefinitionProvider>();
    }
}
