namespace Granit.Settings.Events;

/// <summary>
/// No-op implementation of <see cref="ISettingEventPublisher"/>.
/// Registered by default; replaced when audit log infrastructure is installed.
/// </summary>
internal sealed class NullSettingEventPublisher : ISettingEventPublisher
{
    /// <inheritdoc/>
    public Task PublishAsync(SettingChangedEvent settingChangedEvent, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
