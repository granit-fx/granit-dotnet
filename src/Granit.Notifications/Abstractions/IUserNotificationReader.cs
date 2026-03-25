using Granit.Notifications.Domain;
using Granit.QueryEngine;

namespace Granit.Notifications.Abstractions;

/// <summary>
/// Read operations for in-app user notifications (inbox).
/// </summary>
public interface IUserNotificationReader
{
    Task<UserNotification?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<UserNotification>> GetListAsync(string recipientUserId, Guid? tenantId, int page = 1, int pageSize = QueryEngineDefaults.DefaultPageSize, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(string recipientUserId, Guid? tenantId, CancellationToken cancellationToken = default);
    Task<PagedResult<UserNotification>> GetByEntityAsync(string entityType, string entityId, Guid? tenantId, int page = 1, int pageSize = QueryEngineDefaults.DefaultPageSize, CancellationToken cancellationToken = default);
}
