using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IUserNotificationReader"/> and
/// <see cref="IUserNotificationWriter"/> backed by PostgreSQL.
/// </summary>
internal sealed class EfCoreUserNotificationStore(IDbContextFactory<NotificationsDbContext> dbContextFactory) : IUserNotificationReader, IUserNotificationWriter
{
    /// <inheritdoc/>
    public async Task InsertAsync(UserNotification notification, CancellationToken cancellationToken = default)
    {
        await using NotificationsDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.UserNotifications.Add(notification);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<UserNotification?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using NotificationsDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.UserNotifications.FindAsync([id], cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<UserNotification>> GetListAsync(string recipientUserId, Guid? tenantId, int page = 1, int pageSize = QueryEngineDefaults.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        (int clampedPage, int clampedPageSize) = QueryEngineDefaults.ClampPagination(page, pageSize);

        await using NotificationsDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        IQueryable<UserNotification> query = db.UserNotifications
            .Where(n => n.RecipientUserId == recipientUserId && n.TenantId == tenantId);

        int totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        List<UserNotification> items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((clampedPage - 1) * clampedPageSize)
            .Take(clampedPageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PagedResult<UserNotification>(items, totalCount, HasMore: (clampedPage - 1) * clampedPageSize + items.Count < totalCount);
    }

    /// <inheritdoc/>
    public async Task<int> GetUnreadCountAsync(string recipientUserId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        await using NotificationsDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.UserNotifications
            .CountAsync(n => n.RecipientUserId == recipientUserId && n.TenantId == tenantId && n.State == UserNotificationState.Unread, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task MarkAsReadAsync(Guid id, string recipientUserId, DateTimeOffset readAt, CancellationToken cancellationToken = default)
    {
        await using NotificationsDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        UserNotification? notification = await db.UserNotifications
            .FirstOrDefaultAsync(n => n.Id == id && n.RecipientUserId == recipientUserId, cancellationToken).ConfigureAwait(false);

        if (notification is null)
        {
            return;
        }

        notification.MarkAsRead(readAt);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task MarkAllAsReadAsync(string recipientUserId, Guid? tenantId, DateTimeOffset readAt, CancellationToken cancellationToken = default)
    {
        await using NotificationsDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<UserNotification> unread = await db.UserNotifications
            .Where(n => n.RecipientUserId == recipientUserId && n.TenantId == tenantId && n.State == UserNotificationState.Unread)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (UserNotification notification in unread)
        {
            notification.MarkAsRead(readAt);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<UserNotification>> GetByEntityAsync(string entityType, string entityId, Guid? tenantId, int page = 1, int pageSize = QueryEngineDefaults.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        (int clampedPage, int clampedPageSize) = QueryEngineDefaults.ClampPagination(page, pageSize);

        await using NotificationsDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        IQueryable<UserNotification> query = db.UserNotifications
            .Where(n => n.RelatedEntityType == entityType && n.RelatedEntityId == entityId && n.TenantId == tenantId);

        int totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        List<UserNotification> items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((clampedPage - 1) * clampedPageSize)
            .Take(clampedPageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PagedResult<UserNotification>(items, totalCount, HasMore: (clampedPage - 1) * clampedPageSize + items.Count < totalCount);
    }
}
