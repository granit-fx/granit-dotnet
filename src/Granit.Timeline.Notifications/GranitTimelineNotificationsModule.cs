using Granit.Modularity;
using Granit.Notifications;
using Granit.Templating;
using Granit.Templating.Extensions;
using Granit.Timeline.Notifications.Extensions;

namespace Granit.Timeline.Notifications;

/// <summary>
/// Granit module for the timeline notification bridge.
/// Replaces the default in-memory follower service and null notifier with
/// notification-backed implementations and ships the embedded HTML templates
/// for the email channel of <c>timeline.user_mentioned</c>.
/// </summary>
[DependsOn(
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitTemplatingModule),
    typeof(GranitTimelineModule))]
public sealed class GranitTimelineNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitTimelineNotifications();

        // Ship the embedded HTML templates for the timeline email notifications.
        // Apps can override any of them at runtime through the Granit.Templating
        // admin API (DB-backed resolver runs at higher priority than the embedded one).
        context.Services.AddEmbeddedTemplates(typeof(GranitTimelineNotificationsModule).Assembly);

        // Layout glob — every timeline notification email uses the host's `Layout.Email`
        // wrapper if registered. If absent, templates render without layout (warning logged,
        // no crash).
        context.Services.AddTemplateLayout("timeline.*", "Layout.Email");
    }
}
