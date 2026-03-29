using Granit.Modularity;
using Granit.Notifications;
using Granit.Timeline.Notifications.Extensions;

namespace Granit.Timeline.Notifications;

/// <summary>
/// Granit module for the timeline notification bridge.
/// Replaces the default in-memory follower service and null notifier with
/// notification-backed implementations.
/// </summary>
[DependsOn(
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitTimelineModule))]
public sealed class GranitTimelineNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitTimelineNotifications();
}
