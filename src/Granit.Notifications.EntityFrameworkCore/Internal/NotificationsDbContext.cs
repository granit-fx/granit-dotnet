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
/// EF Core DbContext for notification persistence.
/// </summary>
internal sealed class NotificationsDbContext(
    DbContextOptions<NotificationsDbContext> options,
    IStringEncryptionService encryption,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    private readonly IStringEncryptionService _encryption = encryption;

    /// <summary>In-app user notifications (inbox).</summary>
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    /// <summary>Notification subscriptions (topic + entity followers).</summary>
    public DbSet<NotificationSubscription> Subscriptions => Set<NotificationSubscription>();

    /// <summary>User notification preferences (opt-in/opt-out per channel).</summary>
    public DbSet<NotificationPreference> Preferences => Set<NotificationPreference>();

    /// <summary>Immutable ISO 27001 audit trail of delivery attempts.</summary>
    public DbSet<NotificationDeliveryAttempt> DeliveryAttempts => Set<NotificationDeliveryAttempt>();

    /// <summary>Mobile push device tokens.</summary>
    public DbSet<MobilePushToken> MobilePushTokens => Set<MobilePushToken>();

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ConfigureNotificationsModule();
        modelBuilder.ApplyEncryptionConventions(_encryption);
    }
}
