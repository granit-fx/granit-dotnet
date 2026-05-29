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
/// Tenant-isolated EF Core DbContext for Notifications under
/// <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Segregated"/>. Holds
/// tenant-scoped notifications, preferences, subscriptions, delivery attempts, and push
/// tokens. <c>DROP SCHEMA &lt;tenant&gt; CASCADE</c> lessivage on tenant offboarding is
/// the RGPD Art. 17 primitive enabled by this layout — notification bodies carry PII.
/// </summary>
internal sealed class NotificationsTenantDbContext(
    DbContextOptions<NotificationsTenantDbContext> options,
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
