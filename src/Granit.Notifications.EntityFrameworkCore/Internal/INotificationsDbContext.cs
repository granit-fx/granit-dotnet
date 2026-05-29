using Granit.Notifications.Domain;
using Granit.Notifications.MobilePush.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.EntityFrameworkCore.Internal;

/// <summary>
/// Common shape exposed by every Notifications DbContext flavour so stores can dispatch
/// reads and writes regardless of whether the storage layout is <c>Shared</c> (one
/// context) or <c>Segregated</c> (host context + tenant context).
/// </summary>
/// <remarks>
/// Per ADR-063 the two concrete implementations are:
/// <list type="bullet">
///   <item><see cref="NotificationsHostDbContext"/> — host-pinned. Serves both <c>Shared</c> mode (single context for all rows, row-level filter) and the host portion of <c>Segregated</c> mode (platform-level notifications only).</item>
///   <item><see cref="NotificationsTenantDbContext"/> — tenant-isolated. Used only under <c>Segregated</c> mode for per-tenant notifications.</item>
/// </list>
/// </remarks>
internal interface INotificationsDbContext : IAsyncDisposable, IDisposable
{
    /// <summary>In-app user notifications (inbox).</summary>
    DbSet<UserNotification> UserNotifications { get; }

    /// <summary>Notification subscriptions (topic + entity followers).</summary>
    DbSet<NotificationSubscription> Subscriptions { get; }

    /// <summary>User notification preferences (opt-in/opt-out per channel).</summary>
    DbSet<NotificationPreference> Preferences { get; }

    /// <summary>Immutable ISO 27001 audit trail of delivery attempts.</summary>
    DbSet<NotificationDeliveryAttempt> DeliveryAttempts { get; }

    /// <summary>Mobile push device tokens.</summary>
    DbSet<MobilePushToken> MobilePushTokens { get; }

    /// <summary>Persists pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
