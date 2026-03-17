namespace Granit.Settings.Events;

/// <summary>
/// Publishes <see cref="SettingChangedEvent"/> after setting mutations.
/// </summary>
/// <remarks>
/// Default implementation is <see cref="NullSettingEventPublisher"/> (no-op).
/// Replace with a Wolverine-backed publisher for audit log integration.
/// </remarks>
public interface ISettingEventPublisher
{
    /// <summary>
    /// Publishes a setting change event.
    /// </summary>
    Task PublishAsync(SettingChangedEvent settingChangedEvent, CancellationToken cancellationToken = default);
}
