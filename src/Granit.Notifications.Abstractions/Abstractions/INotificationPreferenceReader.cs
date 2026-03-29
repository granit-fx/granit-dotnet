using Granit.Notifications.Domain;

namespace Granit.Notifications.Abstractions;

/// <summary>
/// Read operations for user notification preferences (opt-in/opt-out per channel/type).
/// </summary>
public interface INotificationPreferenceReader
{
    Task<IReadOnlyList<NotificationPreference>> GetListAsync(string userId, Guid? tenantId, CancellationToken cancellationToken = default);
    Task<NotificationPreference?> GetAsync(string userId, string notificationTypeName, string channelName, Guid? tenantId, CancellationToken cancellationToken = default);
    Task<bool> IsChannelEnabledAsync(string userId, string notificationTypeName, string channelName, Guid? tenantId, CancellationToken cancellationToken = default);
}
