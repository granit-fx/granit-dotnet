using System.Collections.Concurrent;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Granit.Querying;

namespace Granit.Notifications.Internal;

internal sealed class InMemoryUserNotificationStore : IUserNotificationReader, IUserNotificationWriter
{
    private readonly ConcurrentDictionary<Guid, UserNotification> _notifications = new();

    public Task InsertAsync(UserNotification notification, CancellationToken cancellationToken = default)
    {
        _notifications[notification.Id] = notification;
        return Task.CompletedTask;
    }

    public Task<UserNotification?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_notifications.GetValueOrDefault(id));

    public Task<PagedResult<UserNotification>> GetListAsync(string recipientUserId, Guid? tenantId, int page = 1, int pageSize = QueryingDefaults.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        int clampedPage = Math.Max(page, 1);
        int clampedPageSize = Math.Clamp(pageSize, 1, QueryingDefaults.MaxPageSize);

        var filtered = _notifications.Values
            .Where(n => n.RecipientUserId == recipientUserId && n.TenantId == tenantId)
            .OrderByDescending(n => n.CreatedAt)
            .ToList();

        int totalCount = filtered.Count;
        int skip = (clampedPage - 1) * clampedPageSize;
        var items = filtered
            .Skip(skip)
            .Take(clampedPageSize)
            .ToList();

        return Task.FromResult(new PagedResult<UserNotification>(items, totalCount, HasMore: skip + items.Count < totalCount));
    }

    public Task<int> GetUnreadCountAsync(string recipientUserId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        int count = _notifications.Values
            .Count(n => n.RecipientUserId == recipientUserId && n.TenantId == tenantId && n.State == UserNotificationState.Unread);
        return Task.FromResult(count);
    }

    public Task MarkAsReadAsync(Guid id, DateTimeOffset readAt, CancellationToken cancellationToken = default)
    {
        if (_notifications.TryGetValue(id, out UserNotification? notification))
        {
            notification.MarkAsRead(readAt);
        }
        return Task.CompletedTask;
    }

    public Task MarkAllAsReadAsync(string recipientUserId, Guid? tenantId, DateTimeOffset readAt, CancellationToken cancellationToken = default)
    {
        foreach (UserNotification notification in _notifications.Values
            .Where(n => n.RecipientUserId == recipientUserId && n.TenantId == tenantId && n.State == UserNotificationState.Unread))
        {
            notification.MarkAsRead(readAt);
        }
        return Task.CompletedTask;
    }

    public Task<PagedResult<UserNotification>> GetByEntityAsync(string entityType, string entityId, Guid? tenantId, int page = 1, int pageSize = QueryingDefaults.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        int clampedPage = Math.Max(page, 1);
        int clampedPageSize = Math.Clamp(pageSize, 1, QueryingDefaults.MaxPageSize);

        var filtered = _notifications.Values
            .Where(n => n.RelatedEntityType == entityType && n.RelatedEntityId == entityId && n.TenantId == tenantId)
            .OrderByDescending(n => n.CreatedAt)
            .ToList();

        int totalCount = filtered.Count;
        int skip = (clampedPage - 1) * clampedPageSize;
        var items = filtered
            .Skip(skip)
            .Take(clampedPageSize)
            .ToList();

        return Task.FromResult(new PagedResult<UserNotification>(items, totalCount, HasMore: skip + items.Count < totalCount));
    }
}
