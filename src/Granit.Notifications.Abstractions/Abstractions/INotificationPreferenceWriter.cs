using Granit.Notifications.Domain;

namespace Granit.Notifications.Abstractions;

/// <summary>
/// Write operations for user notification preferences (opt-in/opt-out per channel/type).
/// </summary>
public interface INotificationPreferenceWriter
{
    Task SetAsync(NotificationPreference preference, CancellationToken cancellationToken = default);
}
