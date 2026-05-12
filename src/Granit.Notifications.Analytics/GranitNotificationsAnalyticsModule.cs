using Granit.Analytics;
using Granit.Analytics.Extensions;
using Granit.Modularity;
using Granit.Notifications.Analytics.Metrics;
using Granit.Notifications.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Notifications.Analytics;

/// <summary>
/// Granit module for the Notifications analytics satellite. Registers the
/// MetricDefinitions observing user-notification volumes (total, unread, read)
/// so analytics hosts can surface them in dashboards and admin pages.
/// </summary>
[DependsOn(
    typeof(GranitNotificationsModule),
    typeof(GranitAnalyticsAbstractionsModule))]
public sealed class GranitNotificationsAnalyticsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMetricDefinition<UserNotification, int, UserNotificationCountMetricDefinition>();
        context.Services.AddMetricDefinition<UserNotification, int, UnreadUserNotificationCountMetricDefinition>();
        context.Services.AddMetricDefinition<UserNotification, int, ReadUserNotificationCountMetricDefinition>();
    }
}
