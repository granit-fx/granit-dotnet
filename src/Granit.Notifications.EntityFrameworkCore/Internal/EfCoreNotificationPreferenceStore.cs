using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="INotificationPreferenceReader"/> and
/// <see cref="INotificationPreferenceWriter"/> backed by PostgreSQL.
/// </summary>
internal sealed class EfCoreNotificationPreferenceStore(
    IDbContextFactory<NotificationsDbContext> contextFactory)
    : EfStoreBase<NotificationPreference, NotificationsDbContext>(contextFactory), INotificationPreferenceReader, INotificationPreferenceWriter
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<NotificationPreference>> GetListAsync(string userId, Guid? tenantId, CancellationToken cancellationToken = default) =>
        ReadAsync(async db =>
            (IReadOnlyList<NotificationPreference>)await db.Preferences
                .Where(p => p.UserId == userId && p.TenantId == tenantId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    /// <inheritdoc/>
    public Task<NotificationPreference?> GetAsync(string userId, string notificationTypeName, string channelName, Guid? tenantId, CancellationToken cancellationToken = default) =>
        FirstOrDefaultAsync(p => p.UserId == userId && p.NotificationTypeName == notificationTypeName && p.ChannelName == channelName && p.TenantId == tenantId, cancellationToken);

    /// <inheritdoc/>
    public async Task SetAsync(NotificationPreference preference, CancellationToken cancellationToken = default)
    {
        await WriteAsync(async db =>
        {
            NotificationPreference? existing = await db.Preferences
                .FirstOrDefaultAsync(p => p.UserId == preference.UserId && p.NotificationTypeName == preference.NotificationTypeName && p.ChannelName == preference.ChannelName && p.TenantId == preference.TenantId, cancellationToken)
                .ConfigureAwait(false);

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
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> IsChannelEnabledAsync(string userId, string notificationTypeName, string channelName, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        NotificationPreference? preference = await FirstOrDefaultAsync(
            p => p.UserId == userId && p.NotificationTypeName == notificationTypeName && p.ChannelName == channelName && p.TenantId == tenantId,
            cancellationToken).ConfigureAwait(false);
        return preference?.IsEnabled ?? true; // Default: enabled
    }
}
