using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Granit.Persistence;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IUserNotificationReader"/> and
/// <see cref="IUserNotificationWriter"/> backed by PostgreSQL.
/// </summary>
internal sealed class EfCoreUserNotificationStore(
    IDbContextFactory<NotificationsDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : EfStoreBase<UserNotification, NotificationsDbContext>(contextFactory, currentTenant), IUserNotificationReader, IUserNotificationWriter
{
    /// <inheritdoc/>
    public Task InsertAsync(UserNotification notification, CancellationToken cancellationToken = default) =>
        AddAsync(notification, cancellationToken);

    /// <inheritdoc/>
    public Task<UserNotification?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        FindByIdAsync(id, cancellationToken);

    /// <inheritdoc/>
    public Task<PagedResult<UserNotification>> GetListAsync(string recipientUserId, Guid? tenantId, int page = 1, int pageSize = QueryEngineDefaults.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        (int clampedPage, int clampedPageSize) = QueryEngineDefaults.ClampPagination(page, pageSize);

        return PagedAsync(
            Spec.For<UserNotification>()
                .Where(n => n.RecipientUserId == recipientUserId && n.TenantId == tenantId)
                .OrderByDescending(n => n.CreatedAt),
            clampedPage,
            clampedPageSize,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<int> GetUnreadCountAsync(string recipientUserId, Guid? tenantId, CancellationToken cancellationToken = default) =>
        CountAsync(n => n.RecipientUserId == recipientUserId && n.TenantId == tenantId && n.State == UserNotificationState.Unread, cancellationToken);

    /// <inheritdoc/>
    public async Task MarkAsReadAsync(Guid id, string recipientUserId, DateTimeOffset readAt, CancellationToken cancellationToken = default)
    {
        await WriteAsync(async db =>
        {
            UserNotification? notification = await db.UserNotifications
                .FirstOrDefaultAsync(n => n.Id == id && n.RecipientUserId == recipientUserId, cancellationToken)
                .ConfigureAwait(false);

            if (notification is null)
            {
                return;
            }

            notification.MarkAsRead(readAt);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task MarkAllAsReadAsync(string recipientUserId, Guid? tenantId, DateTimeOffset readAt, CancellationToken cancellationToken = default)
    {
        await WriteAsync(async db =>
        {
            List<UserNotification> unread = await db.UserNotifications
                .Where(n => n.RecipientUserId == recipientUserId && n.TenantId == tenantId && n.State == UserNotificationState.Unread)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (UserNotification notification in unread)
            {
                notification.MarkAsRead(readAt);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<PagedResult<UserNotification>> GetByEntityAsync(string entityType, string entityId, Guid? tenantId, int page = 1, int pageSize = QueryEngineDefaults.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        (int clampedPage, int clampedPageSize) = QueryEngineDefaults.ClampPagination(page, pageSize);

        return PagedAsync(
            Spec.For<UserNotification>()
                .Where(n => n.RelatedEntityType == entityType && n.RelatedEntityId == entityId && n.TenantId == tenantId)
                .OrderByDescending(n => n.CreatedAt),
            clampedPage,
            clampedPageSize,
            cancellationToken);
    }
}
