using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IUserNotificationReader"/> and
/// <see cref="IUserNotificationWriter"/>. Dispatches through
/// <see cref="NotificationsContextResolver"/>.
/// </summary>
internal sealed class EfCoreUserNotificationStore(NotificationsContextResolver resolver)
    : IUserNotificationReader, IUserNotificationWriter
{
    /// <inheritdoc/>
    public async Task InsertAsync(UserNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        await using INotificationsDbContext db = await resolver
            .OpenForScopeAsync(notification.TenantId, cancellationToken).ConfigureAwait(false);
        db.UserNotifications.Add(notification);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<UserNotification?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<INotificationsDbContext> contexts = await resolver
            .OpenForUnknownScopeAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (INotificationsDbContext db in contexts)
            {
                UserNotification? hit = await db.UserNotifications
                    .FirstOrDefaultAsync(n => n.Id == id, cancellationToken).ConfigureAwait(false);
                if (hit is not null)
                {
                    return hit;
                }
            }
            return null;
        }
        finally
        {
            await DisposeAllAsync(contexts).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task<PagedResult<UserNotification>> GetListAsync(
        string recipientUserId, Guid? tenantId, int page = 1,
        int pageSize = QueryEngineDefaults.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        (int clampedPage, int clampedPageSize) = QueryEngineDefaults.ClampPagination(page, pageSize);

        await using INotificationsDbContext db = await resolver
            .OpenForScopeAsync(tenantId, cancellationToken).ConfigureAwait(false);

        IQueryable<UserNotification> query = db.UserNotifications
            .Where(n => n.RecipientUserId == recipientUserId && n.TenantId == tenantId)
            .OrderByDescending(n => n.CreatedAt);

        int total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        List<UserNotification> items = await query
            .Skip((clampedPage - 1) * clampedPageSize)
            .Take(clampedPageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PagedResult<UserNotification>(items, total, HasMore: (clampedPage * clampedPageSize) < total);
    }

    /// <inheritdoc/>
    public async Task<int> GetUnreadCountAsync(
        string recipientUserId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        await using INotificationsDbContext db = await resolver
            .OpenForScopeAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return await db.UserNotifications
            .CountAsync(
                n => n.RecipientUserId == recipientUserId
                  && n.TenantId == tenantId
                  && n.State == UserNotificationState.Unread,
                cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task MarkAsReadAsync(
        Guid id, string recipientUserId, DateTimeOffset readAt, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<INotificationsDbContext> contexts = await resolver
            .OpenForUnknownScopeAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (INotificationsDbContext db in contexts)
            {
                UserNotification? notification = await db.UserNotifications
                    .FirstOrDefaultAsync(
                        n => n.Id == id && n.RecipientUserId == recipientUserId,
                        cancellationToken).ConfigureAwait(false);

                if (notification is null)
                {
                    continue;
                }

                notification.MarkAsRead(readAt);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
        }
        finally
        {
            await DisposeAllAsync(contexts).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task MarkAllAsReadAsync(
        string recipientUserId, Guid? tenantId, DateTimeOffset readAt, CancellationToken cancellationToken = default)
    {
        await using INotificationsDbContext db = await resolver
            .OpenForScopeAsync(tenantId, cancellationToken).ConfigureAwait(false);

        List<UserNotification> unread = await db.UserNotifications
            .Where(n => n.RecipientUserId == recipientUserId
                     && n.TenantId == tenantId
                     && n.State == UserNotificationState.Unread)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (UserNotification notification in unread)
        {
            notification.MarkAsRead(readAt);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<UserNotification>> GetByEntityAsync(
        string entityType, string entityId, Guid? tenantId, int page = 1,
        int pageSize = QueryEngineDefaults.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        (int clampedPage, int clampedPageSize) = QueryEngineDefaults.ClampPagination(page, pageSize);

        await using INotificationsDbContext db = await resolver
            .OpenForScopeAsync(tenantId, cancellationToken).ConfigureAwait(false);

        IQueryable<UserNotification> query = db.UserNotifications
            .Where(n => n.RelatedEntityType == entityType
                     && n.RelatedEntityId == entityId
                     && n.TenantId == tenantId)
            .OrderByDescending(n => n.CreatedAt);

        int total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        List<UserNotification> items = await query
            .Skip((clampedPage - 1) * clampedPageSize)
            .Take(clampedPageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PagedResult<UserNotification>(items, total, HasMore: (clampedPage * clampedPageSize) < total);
    }

    private static async Task DisposeAllAsync(IReadOnlyList<INotificationsDbContext> contexts)
    {
        foreach (INotificationsDbContext db in contexts)
        {
            await db.DisposeAsync().ConfigureAwait(false);
        }
    }
}
