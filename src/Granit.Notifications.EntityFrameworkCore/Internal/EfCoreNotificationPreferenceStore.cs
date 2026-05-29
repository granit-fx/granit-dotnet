using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="INotificationPreferenceReader"/> and
/// <see cref="INotificationPreferenceWriter"/>. Dispatches through
/// <see cref="NotificationsContextResolver"/>.
/// </summary>
internal sealed class EfCoreNotificationPreferenceStore(NotificationsContextResolver resolver)
    : INotificationPreferenceReader, INotificationPreferenceWriter
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<NotificationPreference>> GetListAsync(
        string userId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        await using INotificationsDbContext db = await resolver
            .OpenForScopeAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return await db.Preferences
            .Where(p => p.UserId == userId && p.TenantId == tenantId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<NotificationPreference?> GetAsync(
        string userId, string notificationTypeName, string channelName, Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        await using INotificationsDbContext db = await resolver
            .OpenForScopeAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return await db.Preferences
            .FirstOrDefaultAsync(
                p => p.UserId == userId
                  && p.NotificationTypeName == notificationTypeName
                  && p.ChannelName == channelName
                  && p.TenantId == tenantId,
                cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SetAsync(NotificationPreference preference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preference);

        await using INotificationsDbContext db = await resolver
            .OpenForScopeAsync(preference.TenantId, cancellationToken).ConfigureAwait(false);

        NotificationPreference? existing = await db.Preferences
            .FirstOrDefaultAsync(
                p => p.UserId == preference.UserId
                  && p.NotificationTypeName == preference.NotificationTypeName
                  && p.ChannelName == preference.ChannelName
                  && p.TenantId == preference.TenantId,
                cancellationToken).ConfigureAwait(false);

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
    public async Task<bool> IsChannelEnabledAsync(
        string userId, string notificationTypeName, string channelName, Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        NotificationPreference? preference = await GetAsync(
            userId, notificationTypeName, channelName, tenantId, cancellationToken).ConfigureAwait(false);
        return preference?.IsEnabled ?? true; // Default: enabled
    }
}
