using Granit.BackgroundJobs.Notifications.Internal;
using Granit.Modularity;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Granit.Templating;
using Granit.Templating.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.BackgroundJobs.Notifications;

/// <summary>
/// Granit module for the background-jobs notification bridge.
/// Routes <see cref="Granit.BackgroundJobs.Events.BackgroundJobFailureThresholdExceededEto"/>
/// to platform / tenant administrators via <c>Granit.Notifications</c> so SLA-critical
/// recurring batches (compliance reports, billing rollups) cannot fail silently for hours.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitTemplatingModule))]
public sealed class GranitBackgroundJobsNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Ship the embedded HTML templates for every background-jobs notification.
        // Apps can override any of them at runtime through the Granit.Templating
        // admin API (DB-backed resolver runs at higher priority than the embedded one).
        context.Services.AddEmbeddedTemplates(typeof(GranitBackgroundJobsNotificationsModule).Assembly);

        // Layout glob — covers all snake_case "jobs.*" notification names. The host
        // application registers the actual `Layout.Email` template; if absent, templates
        // render without layout (warning logged, no crash).
        context.Services.AddTemplateLayout("jobs.*", "Layout.Email");

        context.Services.AddSingleton<INotificationDefinitionProvider, BackgroundJobsNotificationDefinitionProvider>();
    }
}
