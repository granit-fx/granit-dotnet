using Granit.DataFiltering;
using Granit.Encryption;
using Granit.Encryption.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Notifications.Domain;
using Granit.Notifications.EntityFrameworkCore.Extensions;
using Granit.Notifications.MobilePush.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.EntityFrameworkCore.Internal;

/// <summary>
/// Host-pinned EF Core DbContext for Notifications. Serves both Shared mode (single
/// context for all rows) and the host portion of Segregated mode (platform-level
/// notifications only).
/// </summary>
internal sealed class NotificationsHostDbContext(
    DbContextOptions<NotificationsHostDbContext> options,
    IStringEncryptionService encryption,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter), INotificationsDbContext
{
    private readonly IStringEncryptionService _encryption = encryption;

    /// <inheritdoc/>
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    /// <inheritdoc/>
    public DbSet<NotificationSubscription> Subscriptions => Set<NotificationSubscription>();

    /// <inheritdoc/>
    public DbSet<NotificationPreference> Preferences => Set<NotificationPreference>();

    /// <inheritdoc/>
    public DbSet<NotificationDeliveryAttempt> DeliveryAttempts => Set<NotificationDeliveryAttempt>();

    /// <inheritdoc/>
    public DbSet<MobilePushToken> MobilePushTokens => Set<MobilePushToken>();

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ConfigureNotificationsModule();
        modelBuilder.ApplyEncryptionConventions(_encryption);
    }
}
