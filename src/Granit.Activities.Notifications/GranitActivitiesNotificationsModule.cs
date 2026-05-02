using Granit.Activities.Events;
using Granit.Activities.Notifications.Handlers;
using Granit.Events;
using Granit.Modularity;
using Granit.Notifications;
using Granit.Templating;
using Granit.Templating.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Activities.Notifications;

/// <summary>
/// Granit module for the activities notification bridge — routes the three
/// activity lifecycle events (Assigned, Reminder, Overdue) to the assignee
/// via <c>Granit.Notifications</c>, with embedded HTML templates rendered
/// through <c>Granit.Templating</c>.
/// </summary>
[DependsOn(
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitActivitiesModule),
    typeof(GranitTemplatingModule))]
public sealed class GranitActivitiesNotificationsModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Ship the embedded HTML templates for all 3 activity notifications.
        // Apps can override any of them at runtime through the Granit.Templating
        // admin API (DB-backed resolver runs at higher priority than the embedded one).
        context.Services.AddEmbeddedTemplates(typeof(GranitActivitiesNotificationsModule).Assembly);

        // Layout glob — covers the snake_case activity notification names
        // (activity.assigned / activity.reminder / activity.overdue). The host
        // application registers the actual `Layout.Email` template; if absent,
        // templates render without layout (warning logged, no crash).
        context.Services.AddTemplateLayout("activity.*", "Layout.Email");

        // Wire the three local-event handlers — fired by Granit.Activities
        // (Assigned via the aggregate factory, Reminder + Overdue via the
        // background-jobs companion in story A8).
        context.Services.AddScoped<ILocalEventHandler<ActivityAssignedEvent>, ActivityAssignedHandler>();
        context.Services.AddScoped<ILocalEventHandler<ActivityReminderDueEvent>, ActivityReminderHandler>();
        context.Services.AddScoped<ILocalEventHandler<ActivityOverdueEvent>, ActivityOverdueHandler>();
    }
}
