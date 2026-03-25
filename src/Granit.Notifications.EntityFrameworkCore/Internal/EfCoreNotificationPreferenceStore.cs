using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="INotificationPreferenceReader"/> and
/// <see cref="INotificationPreferenceWriter"/> backed by PostgreSQL.
/// </summary>
internal sealed class EfCoreNotificationPreferenceStore(IDbContextFactory<NotificationsDbContext> dbContextFactory) : INotificationPreferenceReader, INotificationPreferenceWriter
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<NotificationPreference>> GetListAsync(string userId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        await using NotificationsDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Preferences
            .Where(p => p.UserId == userId && p.TenantId == tenantId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<NotificationPreference?> GetAsync(string userId, string notificationTypeName, string channelName, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        await using NotificationsDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Preferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.NotificationTypeName == notificationTypeName && p.ChannelName == channelName && p.TenantId == tenantId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SetAsync(NotificationPreference preference, CancellationToken cancellationToken = default)
    {
        await using NotificationsDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        NotificationPreference? existing = await db.Preferences
            .FirstOrDefaultAsync(p => p.UserId == preference.UserId && p.NotificationTypeName == preference.NotificationTypeName && p.ChannelName == preference.ChannelName && p.TenantId == preference.TenantId, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            existing.IsEnabled = preference.IsEnabled;
            existing.ModifiedAt = preference.ModifiedAt;
            existing.ModifiedBy = preference.ModifiedBy;
        }
        else
        {
            db.Preferences.Add(preference);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> IsChannelEnabledAsync(string userId, string notificationTypeName, string channelName, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        await using NotificationsDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        NotificationPreference? preference = await db.Preferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.NotificationTypeName == notificationTypeName && p.ChannelName == channelName && p.TenantId == tenantId, cancellationToken).ConfigureAwait(false);
        return preference?.IsEnabled ?? true; // Default: enabled
    }
}
