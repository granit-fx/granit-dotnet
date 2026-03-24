using Granit.Modularity;
using Granit.Notifications;
using Granit.Notifications.Endpoints;
using Granit.Notifications.EntityFrameworkCore;

namespace Granit.Bundle.Notifications;

/// <summary>
/// Extension methods on <see cref="GranitBuilder"/> for adding the Notifications bundle.
/// </summary>
public static class GranitBuilderNotificationsExtensions
{
    /// <summary>
    /// Adds the Notifications bundle: Notifications, EntityFrameworkCore store,
    /// Endpoints, Email (SMTP), SignalR.
    /// </summary>
    public static GranitBuilder AddNotifications(this GranitBuilder builder)
    {
        builder.AddModule<GranitNotificationsModule>();
        builder.AddModule<GranitNotificationsEntityFrameworkCoreModule>();
        builder.AddModule<GranitNotificationsEndpointsModule>();
        return builder;
    }
}
