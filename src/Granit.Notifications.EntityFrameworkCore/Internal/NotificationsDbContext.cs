using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Notifications.Domain;
using Granit.Notifications.EntityFrameworkCore.Entities;
using Granit.Notifications.EntityFrameworkCore.Extensions;
using Granit.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core DbContext for notification persistence.
/// </summary>
internal sealed class NotificationsDbContext(
    DbContextOptions<NotificationsDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    /// <summary>In-app user notifications (inbox).</summary>
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    /// <summary>Notification subscriptions (topic + entity followers).</summary>
    public DbSet<NotificationSubscription> Subscriptions => Set<NotificationSubscription>();

    /// <summary>User notification preferences (opt-in/opt-out per channel).</summary>
    public DbSet<NotificationPreference> Preferences => Set<NotificationPreference>();

    /// <summary>Immutable ISO 27001 audit trail of delivery attempts.</summary>
    public DbSet<NotificationDeliveryAttempt> DeliveryAttempts => Set<NotificationDeliveryAttempt>();

    /// <summary>Mobile push device tokens.</summary>
    public DbSet<MobilePushTokenEntity> MobilePushTokens => Set<MobilePushTokenEntity>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureNotificationsModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
