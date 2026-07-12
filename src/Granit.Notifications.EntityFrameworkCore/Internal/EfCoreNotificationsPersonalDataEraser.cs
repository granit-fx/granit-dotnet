using Granit.Notifications.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="INotificationsPersonalDataEraser"/>. Purges the
/// inbox, preferences and subscriptions for a user in a single erasure pass, backed by
/// PostgreSQL.
/// </summary>
internal sealed class EfCoreNotificationsPersonalDataEraser(
    IDbContextFactory<NotificationsDbContext> contextFactory) : INotificationsPersonalDataEraser
{
    public async Task<int> EraseUserDataAsync(
        string userId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        await using NotificationsDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        int affected = await db.UserNotifications
            .Where(n => n.RecipientUserId == userId && n.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        affected += await db.Preferences
            .Where(p => p.UserId == userId && p.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        affected += await db.Subscriptions
            .Where(s => s.UserId == userId && s.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        return affected;
    }
}
