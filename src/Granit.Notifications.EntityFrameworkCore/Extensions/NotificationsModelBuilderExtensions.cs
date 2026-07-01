using Granit.Notifications.EntityFrameworkCore.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit notification entity
/// configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class NotificationsModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit Notifications module.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureNotificationsModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserNotificationConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationSubscriptionConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationPreferenceConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationDeliveryAttemptConfiguration());
        return modelBuilder;
    }
}
