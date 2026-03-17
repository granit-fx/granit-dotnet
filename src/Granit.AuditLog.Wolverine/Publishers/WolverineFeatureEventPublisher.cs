using Granit.Features.Events;
using Wolverine;

namespace Granit.AuditLog.Wolverine.Publishers;

/// <summary>
/// Publishes <see cref="FeatureOverrideChangedEvent"/> to the Wolverine message bus
/// for audit trail persistence and downstream consumers.
/// </summary>
internal sealed class WolverineFeatureEventPublisher(IMessageBus bus) : IFeatureEventPublisher
{
    /// <inheritdoc/>
    public async Task PublishAsync(
        FeatureOverrideChangedEvent featureOverrideChangedEvent,
        CancellationToken cancellationToken = default) =>
        await bus.PublishAsync(featureOverrideChangedEvent).ConfigureAwait(false);
}
