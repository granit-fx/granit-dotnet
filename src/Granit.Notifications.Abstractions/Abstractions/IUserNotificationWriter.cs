using Granit.Notifications.Domain;

namespace Granit.Notifications.Abstractions;

/// <summary>
/// Write operations for in-app user notifications (inbox).
/// </summary>
public interface IUserNotificationWriter
{
    Task InsertAsync(UserNotification notification, CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(Guid id, string recipientUserId, DateTimeOffset readAt, CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(string recipientUserId, Guid? tenantId, DateTimeOffset readAt, CancellationToken cancellationToken = default);
}
