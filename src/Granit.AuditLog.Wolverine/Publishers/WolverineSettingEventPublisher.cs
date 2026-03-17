using Granit.Settings.Events;
using Wolverine;

namespace Granit.AuditLog.Wolverine.Publishers;

/// <summary>
/// Publishes <see cref="SettingChangedEvent"/> to the Wolverine message bus
/// for audit trail persistence and downstream consumers.
/// </summary>
internal sealed class WolverineSettingEventPublisher(IMessageBus bus) : ISettingEventPublisher
{
    /// <inheritdoc/>
    public async Task PublishAsync(
        SettingChangedEvent settingChangedEvent,
        CancellationToken cancellationToken = default) =>
        await bus.PublishAsync(settingChangedEvent).ConfigureAwait(false);
}
